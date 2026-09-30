using SmartShopPOS.Application.Purchasing;
using SmartShopPOS.Contracts.Purchasing;

namespace SmartShopPOS.Api.Purchasing;

public static class PurchaseOrderEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseOrders(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/purchase-orders")
            .RequireAuthorization()
            .WithTags("Purchase orders");

        group.MapGet("", async (IPurchaseOrderService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ListAsync(cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("ListPurchaseOrders")
        .Produces<IReadOnlyList<PurchaseOrderSummaryResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("", async (PurchaseOrderUpsertRequest request, IPurchaseOrderService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/purchase-orders/{result.Value!.Id}", result.Value)
                : Failure(result.Error, result.Message);
        })
        .WithName("CreatePurchaseOrder")
        .Produces<PurchaseOrderResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{purchaseOrderId:guid}", async (Guid purchaseOrderId, IPurchaseOrderService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(purchaseOrderId, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("GetPurchaseOrder")
        .Produces<PurchaseOrderResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{purchaseOrderId:guid}", async (Guid purchaseOrderId, PurchaseOrderUpsertRequest request, IPurchaseOrderService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(purchaseOrderId, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("UpdatePurchaseOrder")
        .Produces<PurchaseOrderResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{purchaseOrderId:guid}/submit", async (Guid purchaseOrderId, IPurchaseOrderService service, CancellationToken cancellationToken) =>
        {
            var result = await service.SubmitAsync(purchaseOrderId, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("SubmitPurchaseOrder")
        .Produces<PurchaseOrderResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{purchaseOrderId:guid}/cancel", async (Guid purchaseOrderId, IPurchaseOrderService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CancelAsync(purchaseOrderId, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("CancelPurchaseOrder")
        .Produces<PurchaseOrderResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static IResult Failure(PurchaseOrderError error, string? message)
    {
        var (status, title) = error switch
        {
            PurchaseOrderError.Unauthenticated => (StatusCodes.Status401Unauthorized, "Authentication required"),
            PurchaseOrderError.Forbidden => (StatusCodes.Status403Forbidden, "Permission denied"),
            PurchaseOrderError.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
            PurchaseOrderError.Conflict or PurchaseOrderError.InactiveBranch or PurchaseOrderError.InactiveSupplier =>
                (StatusCodes.Status409Conflict, "Purchase order conflict"),
            _ => (StatusCodes.Status400BadRequest, "Invalid request")
        };

        return Results.Problem(statusCode: status, title: title, detail: message);
    }
}
