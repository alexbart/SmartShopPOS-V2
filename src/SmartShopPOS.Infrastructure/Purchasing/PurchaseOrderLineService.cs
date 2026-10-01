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

public sealed class PurchaseOrderLineService(
    SmartShopPosDbContext dbContext,
    ICurrentUser currentUser,
    IPermissionChecker permissionChecker,
    IBranchAccessService branchAccessService) : IPurchaseOrderLineService
{
    public async Task<PurchaseOrderLineResult<IReadOnlyList<PurchaseOrderLineResponse>>> ListAsync(
        Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("purchase_orders.lines.view", cancellationToken);
        if (authorization is not null)
            return PurchaseOrderLineResult<IReadOnlyList<PurchaseOrderLineResponse>>.Failure(authorization.Value, Message(authorization.Value));

        var order = await LoadOrderAsync(purchaseOrderId, tracked: false, cancellationToken);
        if (order is null)
            return PurchaseOrderLineResult<IReadOnlyList<PurchaseOrderLineResponse>>.Failure(PurchaseOrderLineError.NotFound, "Purchase order not found.");
        if (!await branchAccessService.CanOperateInBranchAsync(order.BranchId, "purchase_orders.lines.view", cancellationToken))
            return PurchaseOrderLineResult<IReadOnlyList<PurchaseOrderLineResponse>>.Failure(PurchaseOrderLineError.Forbidden, BranchAccessMessage);

        var responses = await LoadLineProgressAsync(purchaseOrderId, cancellationToken);
        return PurchaseOrderLineResult<IReadOnlyList<PurchaseOrderLineResponse>>.Success(responses);
    }

    public async Task<PurchaseOrderLineResult<PurchaseOrderReceivingSummaryResponse>> GetReceivingSummaryAsync(
        Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("purchase_orders.lines.view", cancellationToken);
        if (authorization is not null)
            return PurchaseOrderLineResult<PurchaseOrderReceivingSummaryResponse>.Failure(authorization.Value, Message(authorization.Value));

        var order = await LoadOrderAsync(purchaseOrderId, tracked: false, cancellationToken);
        if (order is null)
            return PurchaseOrderLineResult<PurchaseOrderReceivingSummaryResponse>.Failure(PurchaseOrderLineError.NotFound, "Purchase order not found.");
        if (!await branchAccessService.CanOperateInBranchAsync(order.BranchId, "purchase_orders.lines.view", cancellationToken))
            return PurchaseOrderLineResult<PurchaseOrderReceivingSummaryResponse>.Failure(PurchaseOrderLineError.Forbidden, BranchAccessMessage);

        var lines = await LoadLineProgressAsync(purchaseOrderId, cancellationToken);
        var orderedQuantity = lines.Sum(line => line.Quantity);
        var receivedQuantity = lines.Sum(line => line.ReceivedQuantity);
        var receivingState = lines.Count == 0 || receivedQuantity == 0m
            ? "NotReceived"
            : lines.All(line => line.IsFullyReceived) ? "FullyReceived" : "PartiallyReceived";
        var summary = new PurchaseOrderReceivingSummaryResponse(order.Id, order.OrderNumber, order.Status.ToString(),
            receivingState, orderedQuantity, receivedQuantity, orderedQuantity - receivedQuantity, lines);
        return PurchaseOrderLineResult<PurchaseOrderReceivingSummaryResponse>.Success(summary);
    }

    private async Task<IReadOnlyList<PurchaseOrderLineResponse>> LoadLineProgressAsync(
        Guid purchaseOrderId, CancellationToken cancellationToken)
    {
        var organizationId = currentUser.OrganizationId!.Value;
        var lines = await dbContext.PurchaseOrderLines.AsNoTracking()
            .Include(line => line.Product)
            .Where(line => line.OrganizationId == organizationId && line.PurchaseOrderId == purchaseOrderId)
            .OrderBy(line => line.CreatedAt)
            .ToListAsync(cancellationToken);

        var receivedTotals = await dbContext.GoodsReceiptLines.AsNoTracking()
            .Where(receiptLine => receiptLine.OrganizationId == organizationId && receiptLine.PurchaseOrderId == purchaseOrderId)
            .GroupBy(receiptLine => receiptLine.PurchaseOrderLineId)
            .ToDictionaryAsync(group => group.Key, group => group.Sum(receiptLine => receiptLine.QuantityReceived), cancellationToken);

        return lines.Select(line => ToResponse(line, line.Product, receivedTotals.GetValueOrDefault(line.Id))).ToList();
    }

    public async Task<PurchaseOrderLineResult<PurchaseOrderLineResponse>> CreateAsync(
        Guid purchaseOrderId, PurchaseOrderLineUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("purchase_orders.lines.create", cancellationToken);
        if (authorization is not null)
            return PurchaseOrderLineResult<PurchaseOrderLineResponse>.Failure(authorization.Value, Message(authorization.Value));
        var validation = Validate(request);
        if (validation is not null)
            return PurchaseOrderLineResult<PurchaseOrderLineResponse>.Failure(PurchaseOrderLineError.Invalid, validation);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await LockAndLoadOrderAsync(purchaseOrderId, cancellationToken);
            var access = await CheckOrderAccessAsync(order, "purchase_orders.lines.create", cancellationToken);
            if (access is not null)
                return PurchaseOrderLineResult<PurchaseOrderLineResponse>.Failure(access.Value, Message(access.Value));
            if (order!.Status != PurchaseOrderStatus.Draft)
                return LifecycleConflict();

            var product = await ValidateProductAsync(request.ProductId, cancellationToken);
            if (product.Error != PurchaseOrderLineError.None)
                return PurchaseOrderLineResult<PurchaseOrderLineResponse>.Failure(product.Error, product.Message!);
            if (await dbContext.PurchaseOrderLines.AnyAsync(line => line.OrganizationId == order.OrganizationId &&
                    line.PurchaseOrderId == order.Id && line.ProductId == request.ProductId, cancellationToken))
                return DuplicateProduct();

            PurchaseOrderLine line;
            try
            {
                line = new PurchaseOrderLine(order.OrganizationId, order.Id, request.ProductId,
                    request.Quantity, request.UnitCost, request.Notes);
            }
            catch (DomainException ex)
            {
                return PurchaseOrderLineResult<PurchaseOrderLineResponse>.Failure(PurchaseOrderLineError.Invalid, ex.Message);
            }

            dbContext.PurchaseOrderLines.Add(line);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PurchaseOrderLineResult<PurchaseOrderLineResponse>.Success(ToResponse(line, product.Value!));
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            return DuplicateProduct();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ConcurrencyConflict<PurchaseOrderLineResponse>();
        }
        catch (DbUpdateException ex) when (IsSerializationFailure(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            return ConcurrencyConflict<PurchaseOrderLineResponse>();
        }
    }

    public async Task<PurchaseOrderLineResult<PurchaseOrderLineResponse>> UpdateAsync(
        Guid purchaseOrderId, Guid lineId, PurchaseOrderLineUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("purchase_orders.lines.update", cancellationToken);
        if (authorization is not null)
            return PurchaseOrderLineResult<PurchaseOrderLineResponse>.Failure(authorization.Value, Message(authorization.Value));
        var validation = Validate(request);
        if (validation is not null)
            return PurchaseOrderLineResult<PurchaseOrderLineResponse>.Failure(PurchaseOrderLineError.Invalid, validation);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await LockAndLoadOrderAsync(purchaseOrderId, cancellationToken);
            var access = await CheckOrderAccessAsync(order, "purchase_orders.lines.update", cancellationToken);
            if (access is not null)
                return PurchaseOrderLineResult<PurchaseOrderLineResponse>.Failure(access.Value, Message(access.Value));
            if (order!.Status != PurchaseOrderStatus.Draft)
                return LifecycleConflict();

            var line = await dbContext.PurchaseOrderLines.SingleOrDefaultAsync(candidate =>
                candidate.Id == lineId && candidate.OrganizationId == order.OrganizationId && candidate.PurchaseOrderId == order.Id,
                cancellationToken);
            if (line is null)
                return PurchaseOrderLineResult<PurchaseOrderLineResponse>.Failure(PurchaseOrderLineError.NotFound, "Purchase order line not found.");

            var product = await ValidateProductAsync(request.ProductId, cancellationToken);
            if (product.Error != PurchaseOrderLineError.None)
                return PurchaseOrderLineResult<PurchaseOrderLineResponse>.Failure(product.Error, product.Message!);
            if (await dbContext.PurchaseOrderLines.AnyAsync(candidate => candidate.OrganizationId == order.OrganizationId &&
                    candidate.PurchaseOrderId == order.Id && candidate.ProductId == request.ProductId && candidate.Id != line.Id,
                    cancellationToken))
                return DuplicateProduct();

            try
            {
                line.Update(request.ProductId, request.Quantity, request.UnitCost, request.Notes);
            }
            catch (DomainException ex)
            {
                return PurchaseOrderLineResult<PurchaseOrderLineResponse>.Failure(PurchaseOrderLineError.Invalid, ex.Message);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PurchaseOrderLineResult<PurchaseOrderLineResponse>.Success(ToResponse(line, product.Value!));
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            return DuplicateProduct();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ConcurrencyConflict<PurchaseOrderLineResponse>();
        }
        catch (DbUpdateException ex) when (IsSerializationFailure(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            return ConcurrencyConflict<PurchaseOrderLineResponse>();
        }
    }

    public async Task<PurchaseOrderLineResult<bool>> DeleteAsync(
        Guid purchaseOrderId, Guid lineId, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("purchase_orders.lines.delete", cancellationToken);
        if (authorization is not null)
            return PurchaseOrderLineResult<bool>.Failure(authorization.Value, Message(authorization.Value));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await LockAndLoadOrderAsync(purchaseOrderId, cancellationToken);
            var access = await CheckOrderAccessAsync(order, "purchase_orders.lines.delete", cancellationToken);
            if (access is not null)
                return PurchaseOrderLineResult<bool>.Failure(access.Value, Message(access.Value));
            if (order!.Status != PurchaseOrderStatus.Draft)
                return PurchaseOrderLineResult<bool>.Failure(PurchaseOrderLineError.Conflict, LifecycleMessage);

            var line = await dbContext.PurchaseOrderLines.SingleOrDefaultAsync(candidate =>
                candidate.Id == lineId && candidate.OrganizationId == order.OrganizationId && candidate.PurchaseOrderId == order.Id,
                cancellationToken);
            if (line is null)
                return PurchaseOrderLineResult<bool>.Failure(PurchaseOrderLineError.NotFound, "Purchase order line not found.");

            dbContext.PurchaseOrderLines.Remove(line);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PurchaseOrderLineResult<bool>.Success(true);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ConcurrencyConflict<bool>();
        }
        catch (DbUpdateException ex) when (IsSerializationFailure(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            return ConcurrencyConflict<bool>();
        }
    }

    private async Task<PurchaseOrder?> LockAndLoadOrderAsync(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = currentUser.OrganizationId!.Value;
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction!.GetDbTransaction();
            command.CommandText = "SELECT \"Id\" FROM purchase_orders WHERE \"OrganizationId\" = @organizationId AND \"Id\" = @purchaseOrderId FOR UPDATE;";
            var organizationParameter = command.CreateParameter();
            organizationParameter.ParameterName = "organizationId";
            organizationParameter.Value = organizationId;
            command.Parameters.Add(organizationParameter);
            var orderParameter = command.CreateParameter();
            orderParameter.ParameterName = "purchaseOrderId";
            orderParameter.Value = id;
            command.Parameters.Add(orderParameter);
            await command.ExecuteScalarAsync(cancellationToken);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        return await LoadOrderAsync(id, tracked: true, cancellationToken);
    }

    private Task<PurchaseOrder?> LoadOrderAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<PurchaseOrder> query = dbContext.PurchaseOrders;
        if (!tracked)
            query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(order => order.Id == id && order.OrganizationId == currentUser.OrganizationId,
            cancellationToken);
    }

    private async Task<PurchaseOrderLineError?> CheckOrderAccessAsync(
        PurchaseOrder? order, string permission, CancellationToken cancellationToken)
    {
        if (order is null)
            return PurchaseOrderLineError.NotFound;
        if (!await branchAccessService.CanOperateInBranchAsync(order.BranchId, permission, cancellationToken))
            return PurchaseOrderLineError.Forbidden;
        return null;
    }

    private async Task<ProductResult> ValidateProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(candidate =>
            candidate.Id == productId && candidate.OrganizationId == currentUser.OrganizationId, cancellationToken);
        if (product is null)
            return new(PurchaseOrderLineError.NotFound, "Product not found.", null);
        if (!product.IsActive)
            return new(PurchaseOrderLineError.InactiveProduct, "Inactive products cannot be added to purchase order lines.", null);
        return new(PurchaseOrderLineError.None, null, product);
    }

    private async Task<PurchaseOrderLineError?> AuthorizeAsync(string permission, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty ||
            currentUser.OrganizationId is not Guid organizationId || organizationId == Guid.Empty)
            return PurchaseOrderLineError.Unauthenticated;
        return await permissionChecker.HasPermissionAsync(userId, permission, cancellationToken)
            ? null : PurchaseOrderLineError.Forbidden;
    }

    private static string? Validate(PurchaseOrderLineUpsertRequest? request)
    {
        if (request is null)
            return "A purchase order line request is required.";
        if (request.ProductId == Guid.Empty)
            return "A product is required.";
        if (request.Quantity <= 0m)
            return "Quantity must be greater than zero.";
        if (request.Quantity != decimal.Round(request.Quantity, 4) || request.Quantity >= 100_000_000_000_000m)
            return "Quantity must fit numeric(18,4).";
        if (request.UnitCost < 0m)
            return "Unit cost cannot be negative.";
        if (request.UnitCost != decimal.Round(request.UnitCost, 4) || request.UnitCost >= 100_000_000_000_000m)
            return "Unit cost must fit numeric(18,4).";
        if (request.Notes?.Length > 1000)
            return "Line notes must not exceed 1000 characters.";
        return null;
    }

    private static PurchaseOrderLineResponse ToResponse(PurchaseOrderLine line, Product product, decimal receivedQuantity = 0m) => new(
        line.Id, line.PurchaseOrderId, line.ProductId, product.Name, product.Sku,
        line.Quantity, line.UnitCost, line.LineTotal, line.Notes, line.CreatedAt, line.UpdatedAt,
        receivedQuantity, line.Quantity - receivedQuantity, receivedQuantity == line.Quantity);

    private static PurchaseOrderLineResult<PurchaseOrderLineResponse> LifecycleConflict() =>
        PurchaseOrderLineResult<PurchaseOrderLineResponse>.Failure(PurchaseOrderLineError.Conflict, LifecycleMessage);

    private static PurchaseOrderLineResult<PurchaseOrderLineResponse> DuplicateProduct() =>
        PurchaseOrderLineResult<PurchaseOrderLineResponse>.Failure(PurchaseOrderLineError.Conflict,
            "A product can appear only once on a purchase order.");

    private static PurchaseOrderLineResult<T> ConcurrencyConflict<T>() =>
        PurchaseOrderLineResult<T>.Failure(PurchaseOrderLineError.Conflict,
            "The purchase order changed concurrently. Reload it and retry.");

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static bool IsSerializationFailure(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure };

    private const string LifecycleMessage = "Purchase order lines can only be changed while the purchase order is Draft.";
    private const string BranchAccessMessage = "The user is not authorized to operate in the purchase order destination branch context.";

    private static string Message(PurchaseOrderLineError error) => error == PurchaseOrderLineError.Unauthenticated
        ? "Authentication is required."
        : error == PurchaseOrderLineError.NotFound ? "Purchase order not found." : "The required purchase order line permission is missing.";

    private sealed record ProductResult(PurchaseOrderLineError Error, string? Message, Product? Value);
}
