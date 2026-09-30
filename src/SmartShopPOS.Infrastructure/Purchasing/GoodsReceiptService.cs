using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Application.Purchasing;
using SmartShopPOS.Application.UserBranches;
using SmartShopPOS.Contracts.Purchasing;
using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.Infrastructure.Purchasing;

public sealed class GoodsReceiptService(SmartShopPosDbContext db, ICurrentUser currentUser,
    IPermissionChecker permissions, IBranchAccessService branchAccess) : IGoodsReceiptService
{
    public async Task<GoodsReceiptResult<IReadOnlyList<GoodsReceiptResponse>>> ListForOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var auth = await AuthorizeAsync("goods_receipts.view", cancellationToken);
        if (auth is not null) return Fail<IReadOnlyList<GoodsReceiptResponse>>(auth.Value);
        var order = await LoadOrderAsync(purchaseOrderId, cancellationToken);
        if (order is null) return GoodsReceiptResult<IReadOnlyList<GoodsReceiptResponse>>.Failure(GoodsReceiptError.NotFound, "Purchase order not found.");
        if (!await branchAccess.CanOperateInBranchAsync(order.BranchId, "goods_receipts.view", cancellationToken))
            return GoodsReceiptResult<IReadOnlyList<GoodsReceiptResponse>>.Failure(GoodsReceiptError.Forbidden, "Branch access is required for this purchase order.");
        var receipts = await db.GoodsReceipts.AsNoTracking().Where(x => x.OrganizationId == currentUser.OrganizationId && x.PurchaseOrderId == order.Id)
            .OrderBy(x => x.ReceivedAt).Select(x => x.Id).ToListAsync(cancellationToken);
        var values = new List<GoodsReceiptResponse>();
        foreach (var id in receipts) values.Add((await GetReceiptResponseAsync(id, cancellationToken))!);
        return GoodsReceiptResult<IReadOnlyList<GoodsReceiptResponse>>.Success(values);
    }

    public async Task<GoodsReceiptResult<GoodsReceiptResponse>> GetAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var auth = await AuthorizeAsync("goods_receipts.view", cancellationToken);
        if (auth is not null) return Fail<GoodsReceiptResponse>(auth.Value);
        var receipt = await db.GoodsReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == receiptId && x.OrganizationId == currentUser.OrganizationId, cancellationToken);
        if (receipt is null) return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.NotFound, "Goods receipt not found.");
        var order = await LoadOrderAsync(receipt.PurchaseOrderId, cancellationToken);
        if (order is null) return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.NotFound, "Goods receipt not found.");
        if (!await branchAccess.CanOperateInBranchAsync(order.BranchId, "goods_receipts.view", cancellationToken))
            return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.Forbidden, "Branch access is required for this receipt.");
        return GoodsReceiptResult<GoodsReceiptResponse>.Success((await GetReceiptResponseAsync(receiptId, cancellationToken))!);
    }

    public async Task<GoodsReceiptResult<GoodsReceiptResponse>> CreateAsync(Guid purchaseOrderId, CreateGoodsReceiptRequest request,
        string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        var auth = await AuthorizeAsync("goods_receipts.create", cancellationToken);
        if (auth is not null) return Fail<GoodsReceiptResponse>(auth.Value);
        var validation = ValidateRequest(request, idempotencyKey);
        if (validation is not null) return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.Invalid, validation);
        var organizationId = currentUser.OrganizationId!.Value;
        idempotencyKey = idempotencyKey!.Trim();
        var requestHash = Hash(request);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var existing = await db.GoodsReceiptIdempotency.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Key == idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (existing.RequestHash != requestHash) return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.Conflict, "Idempotency key was already used with a different request.");
                var previous = await GetReceiptResponseAsync(existing.GoodsReceiptId, cancellationToken);
                return previous is null ? GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.Conflict, "Idempotency record has no receipt.") : GoodsReceiptResult<GoodsReceiptResponse>.Success(previous, true);
            }

            var order = await LockAndLoadOrderAsync(purchaseOrderId, cancellationToken);
            if (order is null) return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.NotFound, "Purchase order not found.");
            if (!await branchAccess.CanOperateInBranchAsync(order.BranchId, "goods_receipts.create", cancellationToken))
                return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.Forbidden, "Branch access is required for this purchase order.");
            if (order.Status != PurchaseOrderStatus.Submitted)
                return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.Conflict, "Only submitted purchase orders can be received.");
            var branch = await db.Branches.SingleOrDefaultAsync(x => x.Id == order.BranchId && x.OrganizationId == organizationId, cancellationToken);
            if (branch is null) return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.NotFound, "Purchase order not found.");
            if (!branch.IsActive) return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.Conflict, "Inactive branches cannot receive stock.");

            var ids = request.Lines.Select(x => x.PurchaseOrderLineId).ToArray();
            var lines = await db.PurchaseOrderLines.Include(x => x.Product)
                .Where(x => x.OrganizationId == organizationId && x.PurchaseOrderId == purchaseOrderId && ids.Contains(x.Id))
                .OrderBy(x => x.Id).ToListAsync(cancellationToken);
            if (lines.Count != ids.Length) return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.Invalid, "Every receipt line must reference a line on this purchase order.");
            if (lines.Any(x => !x.Product.IsActive)) return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.Conflict, "Inactive products cannot receive inventory.");
            var lineIds = lines.Select(x => x.Id).ToArray();
            var totals = await db.GoodsReceiptLines.Where(x => x.OrganizationId == organizationId && lineIds.Contains(x.PurchaseOrderLineId))
                .GroupBy(x => x.PurchaseOrderLineId).Select(x => new { Id = x.Key, Quantity = x.Sum(y => y.QuantityReceived) }).ToDictionaryAsync(x => x.Id, x => x.Quantity, cancellationToken);
            foreach (var requested in request.Lines)
            {
                var line = lines.Single(x => x.Id == requested.PurchaseOrderLineId);
                if (totals.GetValueOrDefault(line.Id) + requested.QuantityReceived > line.Quantity)
                    return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.Conflict, $"Receiving would exceed ordered quantity for product {line.Product.Name}.");
            }

            var receiptId = Guid.NewGuid();
            var receiptNumber = await NextReceiptNumberAsync(organizationId, cancellationToken);
            var number = $"GR-{receiptNumber:D6}";
            var receipt = new GoodsReceipt(organizationId, order.Id, number, request.ReceivedAt, request.Notes, currentUser.UserId!.Value, receiptId);
            db.GoodsReceipts.Add(receipt);
            db.GoodsReceiptIdempotency.Add(new GoodsReceiptIdempotency(organizationId, idempotencyKey!, requestHash, receiptId));
            foreach (var requested in request.Lines)
            {
                var poLine = lines.Single(x => x.Id == requested.PurchaseOrderLineId);
                db.GoodsReceiptLines.Add(new GoodsReceiptLine(organizationId, receiptId, order.Id, poLine.Id, requested.QuantityReceived));
                ApplyReceiptToInventory(organizationId, order.BranchId, poLine.ProductId, requested.QuantityReceived,
                    request.ReceivedAt, receiptId, currentUser.UserId.Value);
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return GoodsReceiptResult<GoodsReceiptResponse>.Success((await GetReceiptResponseAsync(receiptId, cancellationToken))!);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(cancellationToken);
            var previous = await db.GoodsReceiptIdempotency.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Key == idempotencyKey, cancellationToken);
            if (previous is not null && previous.RequestHash == requestHash)
            {
                var response = await GetReceiptResponseAsync(previous.GoodsReceiptId, cancellationToken);
                if (response is not null) return GoodsReceiptResult<GoodsReceiptResponse>.Success(response, true);
            }
            return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.Conflict, "Receipt number or idempotency key conflicts with an existing record.");
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        {
            await transaction.RollbackAsync(cancellationToken);
            return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.Conflict, "The purchase order changed concurrently. Reload and retry.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected })
        {
            await transaction.RollbackAsync(cancellationToken);
            return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.Conflict, "The purchase order changed concurrently. Reload and retry.");
        }
        catch (InvalidOperationException ex) when (HasSerializationFailure(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            return GoodsReceiptResult<GoodsReceiptResponse>.Failure(GoodsReceiptError.Conflict, "The purchase order changed concurrently. Reload and retry.");
        }
    }

    private void ApplyReceiptToInventory(Guid org, Guid branch, Guid product, decimal quantity, DateTimeOffset occurredAt, Guid receiptId, Guid userId)
    {
        var balance = db.InventoryBalances.SingleOrDefault(x => x.OrganizationId == org && x.BranchId == branch && x.ProductId == product);
        var delta = StockMovement.GetDelta(MovementType.Receipt) * quantity;
        if (balance is null) db.InventoryBalances.Add(new InventoryBalance(org, branch, product, delta));
        else balance.AdjustQuantity(delta);
        db.StockMovements.Add(new StockMovement(org, branch, product, MovementType.Receipt, quantity,
            occurredAt, "GoodsReceipt", receiptId, null, userId));
    }

    private async Task<GoodsReceiptResponse?> GetReceiptResponseAsync(Guid id, CancellationToken cancellationToken)
    {
        var receipt = await db.GoodsReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == currentUser.OrganizationId, cancellationToken);
        if (receipt is null) return null;
        var order = await db.PurchaseOrders.AsNoTracking().Include(x => x.Supplier).Include(x => x.Branch)
            .SingleOrDefaultAsync(x => x.Id == receipt.PurchaseOrderId && x.OrganizationId == receipt.OrganizationId, cancellationToken);
        if (order is null) return null;
        var lines = await (from receiptLine in db.GoodsReceiptLines.AsNoTracking()
                           join poLine in db.PurchaseOrderLines.AsNoTracking() on new { receiptLine.OrganizationId, receiptLine.PurchaseOrderLineId } equals new { poLine.OrganizationId, PurchaseOrderLineId = poLine.Id }
                           join product in db.Products.AsNoTracking() on new { poLine.OrganizationId, ProductId = poLine.ProductId } equals new { product.OrganizationId, ProductId = product.Id }
                           where receiptLine.OrganizationId == receipt.OrganizationId && receiptLine.GoodsReceiptId == receipt.Id
                           select new GoodsReceiptLineResponse(receiptLine.Id, poLine.Id, product.Id, product.Name, receiptLine.QuantityReceived,
                               db.GoodsReceiptLines.Where(x => x.OrganizationId == receipt.OrganizationId && x.PurchaseOrderLineId == poLine.Id).Sum(x => (decimal?)x.QuantityReceived) ?? 0m))
            .ToListAsync(cancellationToken);
        return new GoodsReceiptResponse(receipt.Id, receipt.ReceiptNumber, order.Id, order.OrderNumber, order.SupplierId,
            order.Supplier.Name, order.BranchId, order.Branch.Name, receipt.ReceivedAt, receipt.Notes, receipt.CreatedByUserId, receipt.CreatedAt, lines);
    }

    private async Task<PurchaseOrder?> LoadOrderAsync(Guid id, CancellationToken token) => await db.PurchaseOrders.AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == currentUser.OrganizationId, token);

    private async Task<PurchaseOrder?> LockAndLoadOrderAsync(Guid id, CancellationToken token)
    {
        await db.Database.OpenConnectionAsync(token);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            command.CommandText = "SELECT \"Id\" FROM purchase_orders WHERE \"OrganizationId\" = @org AND \"Id\" = @id FOR UPDATE;";
            var org = command.CreateParameter(); org.ParameterName = "org"; org.Value = currentUser.OrganizationId!.Value; command.Parameters.Add(org);
            var po = command.CreateParameter(); po.ParameterName = "id"; po.Value = id; command.Parameters.Add(po);
            await command.ExecuteScalarAsync(token);
        }
        finally { await db.Database.CloseConnectionAsync(); }
        return await db.PurchaseOrders.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == currentUser.OrganizationId, token);
    }

    private async Task<long> NextReceiptNumberAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = """
            INSERT INTO goods_receipt_number_sequences ("OrganizationId", "LastNumber")
            VALUES (@organizationId, 1)
            ON CONFLICT ("OrganizationId") DO UPDATE
            SET "LastNumber" = goods_receipt_number_sequences."LastNumber" + 1
            RETURNING "LastNumber";
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "organizationId";
        parameter.Value = organizationId;
        command.Parameters.Add(parameter);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private async Task<GoodsReceiptError?> AuthorizeAsync(string permission, CancellationToken token)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty || currentUser.OrganizationId is not Guid org || org == Guid.Empty)
            return GoodsReceiptError.Unauthenticated;
        return await permissions.HasPermissionAsync(userId, permission, token) ? null : GoodsReceiptError.Forbidden;
    }

    private static string? ValidateRequest(CreateGoodsReceiptRequest? request, string? key)
    {
        if (request is null) return "A goods receipt request is required.";
        if (request.ReceivedAt == default) return "ReceivedAt is required.";
        if (request.Notes?.Length > 1000) return "Receipt notes must not exceed 1000 characters.";
        if (request.Lines is null || request.Lines.Count == 0) return "At least one receipt line is required.";
        if (request.Lines.Any(x => x.PurchaseOrderLineId == Guid.Empty || x.QuantityReceived <= 0m || x.QuantityReceived != decimal.Round(x.QuantityReceived, 4) || x.QuantityReceived >= 100_000_000_000_000m))
            return "Receipt lines require a valid PO line and positive quantity fitting numeric(18,4).";
        if (request.Lines.Select(x => x.PurchaseOrderLineId).Distinct().Count() != request.Lines.Count) return "A purchase order line may appear only once per receipt.";
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length > 128) return "A valid Idempotency-Key header is required (maximum 128 characters).";
        return null;
    }

    private static string Hash(CreateGoodsReceiptRequest request)
    {
        var canonical = string.Join("|", request.ReceivedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), request.Notes?.Trim() ?? "",
            string.Join(";", request.Lines.OrderBy(x => x.PurchaseOrderLineId).Select(x => $"{x.PurchaseOrderLineId:N}:{x.QuantityReceived.ToString("G29", CultureInfo.InvariantCulture)}")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static bool HasSerializationFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected })
                return true;
        }
        return false;
    }

    private static GoodsReceiptResult<T> Fail<T>(GoodsReceiptError error) => GoodsReceiptResult<T>.Failure(error, error switch
    {
        GoodsReceiptError.Unauthenticated => "Authentication is required.",
        GoodsReceiptError.Forbidden => "The required goods receipt permission is missing.",
        _ => "The operation is not allowed."
    });
}
