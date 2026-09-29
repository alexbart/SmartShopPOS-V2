using SmartShopPOS.Application.Inventory;
using SmartShopPOS.Contracts.Inventory;

namespace SmartShopPOS.Api.Inventory;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventory(this IEndpointRouteBuilder endpoints)
    {
        var inventory = endpoints.MapGroup("/api/v1/inventory").RequireAuthorization().WithTags("Inventory");

        inventory.MapGet("", async (
            bool? active,
            string? sku,
            string? barcode,
            string? search,
            IInventoryService service,
            CancellationToken cancellationToken) =>
        {
            var filter = new InventoryFilter(active, sku, barcode, search);
            var result = await service.ListBalancesAsync(filter, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("ListInventoryBalancesV1")
        .Produces<IReadOnlyList<InventoryBalanceResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        inventory.MapGet("/{productId:guid}", async (
            Guid productId,
            IInventoryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetBalanceAsync(productId, cancellationToken);
            return result.IsSuccess
                ? result.Value is null ? Results.NotFound() : Results.Ok(result.Value)
                : Failure(result.Error, result.Message);
        })
        .WithName("GetInventoryBalanceV1")
        .Produces<InventoryBalanceResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        inventory.MapGet("/{productId:guid}/movements", async (
            Guid productId,
            IInventoryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListMovementsAsync(productId, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("ListStockMovementsV1")
        .Produces<IReadOnlyList<StockMovementResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        inventory.MapPost("/{productId:guid}/opening-balance", async (
            Guid productId,
            OpeningBalanceRequest request,
            IInventoryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateOpeningBalanceAsync(productId, request, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/inventory/{productId}", result.Value)
                : Failure(result.Error, result.Message);
        })
        .WithName("CreateOpeningBalanceV1")
        .Produces<InventoryBalanceResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        inventory.MapPost("/{productId:guid}/adjustments/increase", async (
            Guid productId,
            AdjustmentRequest request,
            IInventoryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAdjustmentIncreaseAsync(productId, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("CreateAdjustmentIncreaseV1")
        .Produces<InventoryBalanceResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        inventory.MapPost("/{productId:guid}/adjustments/decrease", async (
            Guid productId,
            AdjustmentRequest request,
            IInventoryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAdjustmentDecreaseAsync(productId, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("CreateAdjustmentDecreaseV1")
        .Produces<InventoryBalanceResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static IResult Failure(InventoryError error, string? message)
    {
        var (statusCode, title) = error switch
        {
            InventoryError.Unauthenticated => (StatusCodes.Status401Unauthorized, "Authentication required"),
            InventoryError.Forbidden => (StatusCodes.Status403Forbidden, "Permission denied"),
            InventoryError.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
            InventoryError.Conflict => (StatusCodes.Status409Conflict, "Resource conflict"),
            InventoryError.InsufficientStock => (StatusCodes.Status409Conflict, "Insufficient stock"),
            InventoryError.NoBranchContext => (StatusCodes.Status400BadRequest, "No branch context selected"),
            InventoryError.InactiveBranch => (StatusCodes.Status409Conflict, "Inactive branch"),
            InventoryError.InactiveProduct => (StatusCodes.Status409Conflict, "Inactive product"),
            InventoryError.BranchAssignmentRevoked => (StatusCodes.Status403Forbidden, "Branch assignment revoked"),
            _ => (StatusCodes.Status400BadRequest, "Invalid request")
        };

        return Results.Problem(statusCode: statusCode, title: title, detail: message);
    }
}