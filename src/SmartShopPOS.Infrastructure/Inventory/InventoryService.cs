using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Application.Inventory;
using SmartShopPOS.Application.UserBranches;
using SmartShopPOS.Contracts.Inventory;
using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.Infrastructure.Inventory;

public sealed class InventoryService(
    SmartShopPosDbContext dbContext,
    ICurrentUser currentUser,
    IPermissionChecker permissionChecker,
    IBranchAccessService branchAccessService) : IInventoryService
{
    public async Task<InventoryResult<IReadOnlyList<InventoryBalanceResponse>>> ListBalancesAsync(
        InventoryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("inventory.view", cancellationToken);
        if (authorization is not null)
        {
            return InventoryResult<IReadOnlyList<InventoryBalanceResponse>>.Failure(authorization.Value, Message(authorization.Value));
        }

        var branchAccess = await branchAccessService.CanOperateInBranchAsync(
            currentUser.SelectedBranchId!.Value, "inventory.view", cancellationToken);
        if (!branchAccess)
        {
            var error = await GetBranchAccessErrorAsync(cancellationToken);
            return InventoryResult<IReadOnlyList<InventoryBalanceResponse>>.Failure(error, BranchAccessMessage(error));
        }

        var organizationId = currentUser.OrganizationId!.Value;
        var branchId = currentUser.SelectedBranchId!.Value;

        var query = dbContext.InventoryBalances
            .Include(b => b.Product)
                .ThenInclude(p => p.UnitOfMeasure)
            .Where(balance => balance.OrganizationId == organizationId && balance.BranchId == branchId);

        if (filter.Active is bool active)
        {
            query = query.Where(balance => balance.Product.IsActive == active);
        }

        if (!string.IsNullOrWhiteSpace(filter.Sku))
        {
            var sku = filter.Sku.Trim().ToUpperInvariant();
            query = query.Where(balance => balance.Product.Sku == sku);
        }

        if (!string.IsNullOrWhiteSpace(filter.Barcode))
        {
            var barcode = filter.Barcode.Trim().ToUpperInvariant();
            query = query.Where(balance => balance.Product.Barcode == barcode);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(balance => balance.Product.Name.ToLower().Contains(search) ||
                balance.Product.Sku.ToLower().Contains(search) ||
                balance.Product.Barcode != null && balance.Product.Barcode.ToLower().Contains(search));
        }

        var balances = await query.OrderBy(balance => balance.Product.Sku).ToListAsync(cancellationToken);
        return InventoryResult<IReadOnlyList<InventoryBalanceResponse>>.Success(balances.Select(ToResponse).ToList());
    }

    public async Task<InventoryResult<InventoryBalanceResponse?>> GetBalanceAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("inventory.view", cancellationToken);
        if (authorization is not null)
        {
            return InventoryResult<InventoryBalanceResponse?>.Failure(authorization.Value, Message(authorization.Value));
        }

        var branchAccess = await branchAccessService.CanOperateInBranchAsync(
            currentUser.SelectedBranchId!.Value, "inventory.view", cancellationToken);
        if (!branchAccess)
        {
            var error = await GetBranchAccessErrorAsync(cancellationToken);
            return InventoryResult<InventoryBalanceResponse?>.Failure(error, BranchAccessMessage(error));
        }

        var organizationId = currentUser.OrganizationId!.Value;
        var branchId = currentUser.SelectedBranchId!.Value;

        var balance = await dbContext.InventoryBalances
            .Include(b => b.Product)
                .ThenInclude(p => p.UnitOfMeasure)
            .SingleOrDefaultAsync(
                b => b.OrganizationId == organizationId && b.BranchId == branchId && b.ProductId == productId,
                cancellationToken);

        return balance is null
            ? InventoryResult<InventoryBalanceResponse?>.Success(null)
            : InventoryResult<InventoryBalanceResponse?>.Success(ToResponse(balance));
    }

    public async Task<InventoryResult<IReadOnlyList<StockMovementResponse>>> ListMovementsAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("inventory.movements.view", cancellationToken);
        if (authorization is not null)
        {
            return InventoryResult<IReadOnlyList<StockMovementResponse>>.Failure(authorization.Value, Message(authorization.Value));
        }

        var branchAccess = await branchAccessService.CanOperateInBranchAsync(
            currentUser.SelectedBranchId!.Value, "inventory.movements.view", cancellationToken);
        if (!branchAccess)
        {
            var error = await GetBranchAccessErrorAsync(cancellationToken);
            return InventoryResult<IReadOnlyList<StockMovementResponse>>.Failure(error, BranchAccessMessage(error));
        }

        var organizationId = currentUser.OrganizationId!.Value;
        var branchId = currentUser.SelectedBranchId!.Value;

        var product = await dbContext.Products.SingleOrDefaultAsync(
            p => p.Id == productId && p.OrganizationId == organizationId, cancellationToken);
        if (product is null)
        {
            return InventoryResult<IReadOnlyList<StockMovementResponse>>.Failure(InventoryError.NotFound, "Product not found.");
        }

        var movements = await dbContext.StockMovements
            .Where(m => m.OrganizationId == organizationId && m.BranchId == branchId && m.ProductId == productId)
            .OrderByDescending(m => m.OccurredAt)
            .ThenByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return InventoryResult<IReadOnlyList<StockMovementResponse>>.Success(movements.Select(m => ToResponse(m, product)).ToList());
    }

    public async Task<InventoryResult<InventoryBalanceResponse>> CreateOpeningBalanceAsync(
        Guid productId,
        OpeningBalanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("inventory.opening_balance", cancellationToken);
        if (authorization is not null)
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(authorization.Value, Message(authorization.Value));
        }

        if (request is null)
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(InventoryError.Invalid, "An opening balance request is required.");
        }

        if (!IsValidQuantity(request.Quantity))
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(InventoryError.Invalid, "Opening balance quantity must be positive and fit numeric(18,4).");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(InventoryError.Invalid, "A reason is required for opening balance.");
        }

        var branchAccess = await branchAccessService.CanOperateInBranchAsync(
            currentUser.SelectedBranchId!.Value, "inventory.opening_balance", cancellationToken);
        if (!branchAccess)
        {
            var error = await GetBranchAccessErrorAsync(cancellationToken);
            return InventoryResult<InventoryBalanceResponse>.Failure(error, BranchAccessMessage(error));
        }

        var organizationId = currentUser.OrganizationId!.Value;
        var branchId = currentUser.SelectedBranchId!.Value;
        var userId = currentUser.UserId!.Value;

        // Check if product exists and belongs to organization
        var product = await dbContext.Products.SingleOrDefaultAsync(
            p => p.Id == productId && p.OrganizationId == organizationId, cancellationToken);
        if (product is null)
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(InventoryError.NotFound, "Product not found.");
        }

        if (!product.IsActive)
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(InventoryError.InactiveProduct, "Cannot create opening balance for inactive product.");
        }

        // Check if branch is active
        var branch = await dbContext.Branches.SingleOrDefaultAsync(
            b => b.Id == branchId && b.OrganizationId == organizationId, cancellationToken);
        if (branch is null || !branch.IsActive)
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(InventoryError.InactiveBranch, "Cannot create opening balance for inactive branch.");
        }

        // Check if opening balance already exists for this product at this branch
        var existingBalance = await dbContext.InventoryBalances.SingleOrDefaultAsync(
            b => b.OrganizationId == organizationId && b.BranchId == branchId && b.ProductId == productId, cancellationToken);
        if (existingBalance is not null)
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(InventoryError.Conflict, "An inventory balance already exists for this product at this branch. Use adjustments to modify stock.");
        }

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Create the opening balance movement
            var movement = new StockMovement(
                organizationId,
                branchId,
                productId,
                MovementType.OpeningBalance,
                request.Quantity,
                DateTimeOffset.UtcNow,
                "OPENING_BALANCE",
                null,
                request.Reason,
                userId);

            dbContext.StockMovements.Add(movement);

            // Create the inventory balance
            var balance = new InventoryBalance(organizationId, branchId, productId, request.Quantity);
            dbContext.InventoryBalances.Add(balance);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return InventoryResult<InventoryBalanceResponse>.Success(ToResponse(balance));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(cancellationToken);
            return InventoryResult<InventoryBalanceResponse>.Failure(
                InventoryError.Conflict,
                "An inventory balance already exists for this product at this branch. Use adjustments to modify stock.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<InventoryResult<InventoryBalanceResponse>> CreateAdjustmentIncreaseAsync(
        Guid productId,
        AdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        return await CreateAdjustmentAsync(productId, request, MovementType.AdjustmentIncrease, "inventory.adjust", cancellationToken);
    }

    public async Task<InventoryResult<InventoryBalanceResponse>> CreateAdjustmentDecreaseAsync(
        Guid productId,
        AdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        return await CreateAdjustmentAsync(productId, request, MovementType.AdjustmentDecrease, "inventory.adjust", cancellationToken);
    }

    private async Task<InventoryResult<InventoryBalanceResponse>> CreateAdjustmentAsync(
        Guid productId,
        AdjustmentRequest request,
        MovementType movementType,
        string permission,
        CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeAsync(permission, cancellationToken);
        if (authorization is not null)
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(authorization.Value, Message(authorization.Value));
        }

        if (request is null)
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(InventoryError.Invalid, "An adjustment request is required.");
        }

        if (!IsValidQuantity(request.Quantity))
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(InventoryError.Invalid, "Adjustment quantity must be positive and fit numeric(18,4).");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(InventoryError.Invalid, "A reason is required for adjustments.");
        }

        var branchAccess = await branchAccessService.CanOperateInBranchAsync(
            currentUser.SelectedBranchId!.Value, permission, cancellationToken);
        if (!branchAccess)
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(InventoryError.Forbidden, "Insufficient branch access or permission.");
        }

        var organizationId = currentUser.OrganizationId!.Value;
        var branchId = currentUser.SelectedBranchId!.Value;
        var userId = currentUser.UserId!.Value;

        // Check if product exists and belongs to organization
        var product = await dbContext.Products.SingleOrDefaultAsync(
            p => p.Id == productId && p.OrganizationId == organizationId, cancellationToken);
        if (product is null)
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(InventoryError.NotFound, "Product not found.");
        }

        if (!product.IsActive)
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(InventoryError.InactiveProduct, "Cannot adjust stock for inactive product.");
        }

        // Check if branch is active
        var branch = await dbContext.Branches.SingleOrDefaultAsync(
            b => b.Id == branchId && b.OrganizationId == organizationId, cancellationToken);
        if (branch is null || !branch.IsActive)
        {
            return InventoryResult<InventoryBalanceResponse>.Failure(InventoryError.InactiveBranch, "Cannot adjust stock for inactive branch.");
        }

        // Serializable isolation prevents simultaneous adjustments from overwriting one another.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var balance = await dbContext.InventoryBalances.SingleOrDefaultAsync(
                b => b.OrganizationId == organizationId && b.BranchId == branchId && b.ProductId == productId,
                cancellationToken);

            if (balance is null && movementType == MovementType.AdjustmentDecrease)
            {
                return InventoryResult<InventoryBalanceResponse>.Failure(
                    InventoryError.InsufficientStock,
                    "No inventory balance exists for this product. Cannot decrease stock.");
            }

            // Create the adjustment movement
            var movement = new StockMovement(
                organizationId,
                branchId,
                productId,
                movementType,
                request.Quantity,
                DateTimeOffset.UtcNow,
                "MANUAL_ADJUSTMENT",
                null,
                request.Reason,
                userId);

            dbContext.StockMovements.Add(movement);

            // Update or create the inventory balance
            var delta = StockMovement.GetDelta(movementType) * request.Quantity;

            if (balance is null)
            {
                // Only possible for AdjustmentIncrease
                balance = new InventoryBalance(organizationId, branchId, productId, delta);
                dbContext.InventoryBalances.Add(balance);
            }
            else
            {
                balance.AdjustQuantity(delta);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return InventoryResult<InventoryBalanceResponse>.Success(ToResponse(balance));
        }
        catch (DomainException ex) when (ex.Message == "Insufficient stock.")
        {
            await transaction.RollbackAsync(cancellationToken);
            return InventoryResult<InventoryBalanceResponse>.Failure(InventoryError.InsufficientStock, ex.Message);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(cancellationToken);
            return InventoryResult<InventoryBalanceResponse>.Failure(
                InventoryError.Conflict,
                "The inventory balance changed concurrently. Reload the balance and retry.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return InventoryResult<InventoryBalanceResponse>.Failure(
                InventoryError.Conflict,
                "The inventory balance changed concurrently. Reload the balance and retry.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            await transaction.RollbackAsync(cancellationToken);
            return InventoryResult<InventoryBalanceResponse>.Failure(
                InventoryError.Conflict,
                "The inventory balance changed concurrently. Reload the balance and retry.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<InventoryError?> AuthorizeAsync(string permission, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty ||
            currentUser.OrganizationId is not Guid organizationId || organizationId == Guid.Empty)
        {
            return InventoryError.Unauthenticated;
        }

        if (currentUser.SelectedBranchId is not Guid branchId || branchId == Guid.Empty)
        {
            return InventoryError.NoBranchContext;
        }

        return await permissionChecker.HasPermissionAsync(userId, permission, cancellationToken)
            ? null
            : InventoryError.Forbidden;
    }

    private static string Message(InventoryError error)
    {
        return error == InventoryError.Unauthenticated
            ? "Authentication is required."
            : "The required inventory permission is missing.";
    }

    private async Task<InventoryError> GetBranchAccessErrorAsync(CancellationToken cancellationToken)
    {
        var organizationId = currentUser.OrganizationId!.Value;
        var branchId = currentUser.SelectedBranchId!.Value;
        var branch = await dbContext.Branches.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == branchId && candidate.OrganizationId == organizationId,
            cancellationToken);
        if (branch is { IsActive: false })
        {
            return InventoryError.InactiveBranch;
        }

        var assigned = await dbContext.UserBranches.AsNoTracking().AnyAsync(
            assignment => assignment.OrganizationId == organizationId && assignment.UserId == currentUser.UserId &&
                assignment.BranchId == branchId && assignment.DeactivatedAt == null,
            cancellationToken);
        return !assigned ? InventoryError.BranchAssignmentRevoked : InventoryError.Forbidden;
    }

    private static string BranchAccessMessage(InventoryError error) => error switch
    {
        InventoryError.InactiveBranch => "The selected branch is inactive.",
        InventoryError.BranchAssignmentRevoked => "The user is no longer assigned to the selected branch.",
        _ => "Insufficient branch access or permission."
    };

    private static bool IsValidQuantity(decimal quantity) =>
        quantity > 0m && quantity == decimal.Round(quantity, 4) && quantity < 100_000_000_000_000m;

    private static InventoryBalanceResponse ToResponse(InventoryBalance balance)
    {
        return new InventoryBalanceResponse(
            balance.Id,
            balance.ProductId,
            balance.Product.Sku,
            balance.Product.Name,
            balance.QuantityOnHand,
            balance.CreatedAt,
            balance.UpdatedAt);
    }

    private static StockMovementResponse ToResponse(StockMovement movement, Product product)
    {
        var delta = StockMovement.GetDelta(movement.MovementType) * movement.Quantity;
        return new StockMovementResponse(
            movement.Id,
            movement.ProductId,
            product.Sku,
            product.Name,
            (int)movement.MovementType,
            movement.MovementType.ToString(),
            movement.Quantity,
            delta,
            movement.OccurredAt,
            movement.ReferenceType,
            movement.ReferenceId,
            movement.Reason,
            movement.CreatedByUserId,
            movement.CreatedAt);
    }
}
