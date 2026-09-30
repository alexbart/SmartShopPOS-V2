using System.Data;
using System.Globalization;
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

public sealed class PurchaseOrderService(
    SmartShopPosDbContext dbContext,
    ICurrentUser currentUser,
    IPermissionChecker permissionChecker,
    IBranchAccessService branchAccessService) : IPurchaseOrderService
{
    public async Task<PurchaseOrderResult<IReadOnlyList<PurchaseOrderSummaryResponse>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("purchase_orders.view", cancellationToken);
        if (authorization is not null)
            return PurchaseOrderResult<IReadOnlyList<PurchaseOrderSummaryResponse>>.Failure(authorization.Value, Message(authorization.Value));

        var orders = await dbContext.PurchaseOrders.AsNoTracking()
            .Where(order => order.OrganizationId == currentUser.OrganizationId)
            .OrderByDescending(order => order.OrderDate)
            .ThenBy(order => order.OrderNumber)
            .Select(order => new PurchaseOrderSummaryResponse(
                order.Id, order.OrderNumber, order.SupplierId, order.Supplier.Name, order.BranchId,
                order.Branch.Name, order.Status.ToString(), order.OrderDate, order.ExpectedDate, order.CreatedAt))
            .ToListAsync(cancellationToken);
        return PurchaseOrderResult<IReadOnlyList<PurchaseOrderSummaryResponse>>.Success(orders);
    }

    public async Task<PurchaseOrderResult<PurchaseOrderResponse>> GetAsync(
        Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("purchase_orders.view", cancellationToken);
        if (authorization is not null)
            return PurchaseOrderResult<PurchaseOrderResponse>.Failure(authorization.Value, Message(authorization.Value));

        var order = await LoadOrderAsync(purchaseOrderId, cancellationToken);
        return order is null
            ? PurchaseOrderResult<PurchaseOrderResponse>.Failure(PurchaseOrderError.NotFound, "Purchase order not found.")
            : PurchaseOrderResult<PurchaseOrderResponse>.Success(ToResponse(order));
    }

    public async Task<PurchaseOrderResult<PurchaseOrderResponse>> CreateAsync(
        PurchaseOrderUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("purchase_orders.create", cancellationToken);
        if (authorization is not null)
            return PurchaseOrderResult<PurchaseOrderResponse>.Failure(authorization.Value, Message(authorization.Value));
        var validation = Validate(request);
        if (validation is not null)
            return PurchaseOrderResult<PurchaseOrderResponse>.Failure(PurchaseOrderError.Invalid, validation);

        var organizationId = currentUser.OrganizationId!.Value;
        var userId = currentUser.UserId!.Value;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var supplierResult = await ValidateSupplierAsync(organizationId, request.SupplierId, cancellationToken);
            if (!supplierResult.IsSuccess)
                return PurchaseOrderResult<PurchaseOrderResponse>.Failure(supplierResult.Error, supplierResult.Message!);
            var branchResult = await ValidateBranchAsync(organizationId, request.BranchId, cancellationToken);
            if (!branchResult.IsSuccess)
                return PurchaseOrderResult<PurchaseOrderResponse>.Failure(branchResult.Error, branchResult.Message!);
            if (!await branchAccessService.CanOperateInBranchAsync(request.BranchId, "purchase_orders.create", cancellationToken))
                return PurchaseOrderResult<PurchaseOrderResponse>.Failure(PurchaseOrderError.Forbidden, BranchAccessMessage);

            var number = await NextOrderNumberAsync(organizationId, cancellationToken);
            var order = new PurchaseOrder(organizationId, request.SupplierId, request.BranchId,
                $"PO-{number.ToString("D6", CultureInfo.InvariantCulture)}", request.OrderDate,
                request.ExpectedDate, request.Notes, userId);
            dbContext.PurchaseOrders.Add(order);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PurchaseOrderResult<PurchaseOrderResponse>.Success(
                ToResponse(order, supplierResult.Value!, branchResult.Value!));
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            return PurchaseOrderResult<PurchaseOrderResponse>.Failure(PurchaseOrderError.Conflict,
                "The purchase order number conflicted with another order. Retry the request.");
        }
    }

    public async Task<PurchaseOrderResult<PurchaseOrderResponse>> UpdateAsync(
        Guid purchaseOrderId, PurchaseOrderUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("purchase_orders.update", cancellationToken);
        if (authorization is not null)
            return PurchaseOrderResult<PurchaseOrderResponse>.Failure(authorization.Value, Message(authorization.Value));
        var validation = Validate(request);
        if (validation is not null)
            return PurchaseOrderResult<PurchaseOrderResponse>.Failure(PurchaseOrderError.Invalid, validation);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var order = await LoadOrderAsync(purchaseOrderId, cancellationToken);
            if (order is null)
                return PurchaseOrderResult<PurchaseOrderResponse>.Failure(PurchaseOrderError.NotFound, "Purchase order not found.");
            var orgId = currentUser.OrganizationId!.Value;
            var supplierResult = await ValidateSupplierAsync(orgId, request.SupplierId, cancellationToken);
            if (!supplierResult.IsSuccess)
                return PurchaseOrderResult<PurchaseOrderResponse>.Failure(supplierResult.Error, supplierResult.Message!);
            var branchResult = await ValidateBranchAsync(orgId, request.BranchId, cancellationToken);
            if (!branchResult.IsSuccess)
                return PurchaseOrderResult<PurchaseOrderResponse>.Failure(branchResult.Error, branchResult.Message!);
            if (!await branchAccessService.CanOperateInBranchAsync(request.BranchId, "purchase_orders.update", cancellationToken))
                return PurchaseOrderResult<PurchaseOrderResponse>.Failure(PurchaseOrderError.Forbidden, BranchAccessMessage);

            try
            {
                order.UpdateDraft(request.SupplierId, request.BranchId, request.OrderDate, request.ExpectedDate,
                    request.Notes, currentUser.UserId!.Value);
            }
            catch (DomainException ex)
            {
                return PurchaseOrderResult<PurchaseOrderResponse>.Failure(PurchaseOrderError.Conflict, ex.Message);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PurchaseOrderResult<PurchaseOrderResponse>.Success(ToResponse(order));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ConcurrencyConflict();
        }
        catch (DbUpdateException ex) when (IsSerializationFailure(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            return ConcurrencyConflict();
        }
    }

    public Task<PurchaseOrderResult<PurchaseOrderResponse>> SubmitAsync(
        Guid purchaseOrderId, CancellationToken cancellationToken = default) =>
        TransitionAsync(purchaseOrderId, "purchase_orders.submit", submit: true, cancellationToken);

    public Task<PurchaseOrderResult<PurchaseOrderResponse>> CancelAsync(
        Guid purchaseOrderId, CancellationToken cancellationToken = default) =>
        TransitionAsync(purchaseOrderId, "purchase_orders.cancel", submit: false, cancellationToken);

    private async Task<PurchaseOrderResult<PurchaseOrderResponse>> TransitionAsync(
        Guid purchaseOrderId, string permission, bool submit, CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeAsync(permission, cancellationToken);
        if (authorization is not null)
            return PurchaseOrderResult<PurchaseOrderResponse>.Failure(authorization.Value, Message(authorization.Value));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var order = await LoadOrderAsync(purchaseOrderId, cancellationToken);
            if (order is null)
                return PurchaseOrderResult<PurchaseOrderResponse>.Failure(PurchaseOrderError.NotFound, "Purchase order not found.");
            if (!await branchAccessService.CanOperateInBranchAsync(order.BranchId, permission, cancellationToken))
                return PurchaseOrderResult<PurchaseOrderResponse>.Failure(PurchaseOrderError.Forbidden, BranchAccessMessage);
            try
            {
                if (submit)
                    order.Submit(currentUser.UserId!.Value);
                else
                    order.Cancel(currentUser.UserId!.Value);
            }
            catch (DomainException ex)
            {
                return PurchaseOrderResult<PurchaseOrderResponse>.Failure(PurchaseOrderError.Conflict, ex.Message);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PurchaseOrderResult<PurchaseOrderResponse>.Success(ToResponse(order));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ConcurrencyConflict();
        }
        catch (DbUpdateException ex) when (IsSerializationFailure(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            return ConcurrencyConflict();
        }
    }

    private async Task<long> NextOrderNumberAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction!.GetDbTransaction();
            command.CommandText = """
                INSERT INTO purchase_order_number_sequences ("OrganizationId", "LastNumber")
                VALUES (@organizationId, 1)
                ON CONFLICT ("OrganizationId") DO UPDATE
                SET "LastNumber" = purchase_order_number_sequences."LastNumber" + 1
                RETURNING "LastNumber";
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "organizationId";
            parameter.Value = organizationId;
            command.Parameters.Add(parameter);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private async Task<SupplierResultValue> ValidateSupplierAsync(Guid organizationId, Guid supplierId, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(
            item => item.Id == supplierId && item.OrganizationId == organizationId, cancellationToken);
        if (supplier is null)
            return new(PurchaseOrderError.NotFound, "Supplier not found.", null);
        if (!supplier.IsActive)
            return new(PurchaseOrderError.InactiveSupplier, "Inactive suppliers cannot be selected for a purchase order.", null);
        return new(PurchaseOrderError.None, null, supplier);
    }

    private async Task<BranchResultValue> ValidateBranchAsync(Guid organizationId, Guid branchId, CancellationToken cancellationToken)
    {
        var branch = await dbContext.Branches.SingleOrDefaultAsync(
            item => item.Id == branchId && item.OrganizationId == organizationId, cancellationToken);
        if (branch is null)
            return new(PurchaseOrderError.NotFound, "Branch not found.", null);
        if (!branch.IsActive)
            return new(PurchaseOrderError.InactiveBranch, "Inactive branches cannot be selected for a purchase order.", null);
        return new(PurchaseOrderError.None, null, branch);
    }

    private Task<PurchaseOrder?> LoadOrderAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.PurchaseOrders
            .Include(order => order.Supplier)
            .Include(order => order.Branch)
            .SingleOrDefaultAsync(order => order.Id == id && order.OrganizationId == currentUser.OrganizationId, cancellationToken);

    private async Task<PurchaseOrderError?> AuthorizeAsync(string permission, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty ||
            currentUser.OrganizationId is not Guid organizationId || organizationId == Guid.Empty)
            return PurchaseOrderError.Unauthenticated;
        return await permissionChecker.HasPermissionAsync(userId, permission, cancellationToken)
            ? null : PurchaseOrderError.Forbidden;
    }

    private static string? Validate(PurchaseOrderUpsertRequest? request)
    {
        if (request is null)
            return "A purchase order request is required.";
        if (request.SupplierId == Guid.Empty)
            return "A supplier is required.";
        if (request.BranchId == Guid.Empty)
            return "A destination branch is required.";
        if (request.OrderDate == default)
            return "Order date is required.";
        if (request.ExpectedDate is DateTimeOffset expected && expected == default)
            return "Expected date cannot be an empty date.";
        if (request.Notes?.Length > 1000)
            return "Notes must not exceed 1000 characters.";
        return null;
    }

    private static PurchaseOrderResponse ToResponse(PurchaseOrder order) => ToResponse(order, order.Supplier, order.Branch);

    private static PurchaseOrderResponse ToResponse(PurchaseOrder order, Supplier supplier, Branch branch) => new(
        order.Id, order.OrderNumber, order.SupplierId, supplier.Name, order.BranchId, branch.Name, order.Status.ToString(),
        order.OrderDate, order.ExpectedDate, order.Notes, order.CreatedAt, order.UpdatedAt,
        order.CreatedByUserId, order.UpdatedByUserId);

    private static PurchaseOrderResult<PurchaseOrderResponse> ConcurrencyConflict() =>
        PurchaseOrderResult<PurchaseOrderResponse>.Failure(PurchaseOrderError.Conflict,
            "The purchase order changed concurrently. Reload it and retry.");

    private const string BranchAccessMessage = "The user is not authorized to operate in the destination branch context.";

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static bool IsSerializationFailure(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure };

    private static string Message(PurchaseOrderError error) => error == PurchaseOrderError.Unauthenticated
        ? "Authentication is required." : "The required purchase order permission is missing.";

    private sealed record SupplierResultValue(PurchaseOrderError Error, string? Message, Supplier? Value)
    {
        public bool IsSuccess => Error == PurchaseOrderError.None;
    }

    private sealed record BranchResultValue(PurchaseOrderError Error, string? Message, Branch? Value)
    {
        public bool IsSuccess => Error == PurchaseOrderError.None;
    }
}
