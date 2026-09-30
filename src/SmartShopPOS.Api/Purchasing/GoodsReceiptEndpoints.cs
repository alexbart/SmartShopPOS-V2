using SmartShopPOS.Application.Purchasing;
using SmartShopPOS.Contracts.Purchasing;

namespace SmartShopPOS.Api.Purchasing;

public static class GoodsReceiptEndpoints
{
    public static IEndpointRouteBuilder MapGoodsReceipts(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").RequireAuthorization().WithTags("Goods receipts");
        group.MapGet("/purchase-orders/{purchaseOrderId:guid}/receipts", async (Guid purchaseOrderId, IGoodsReceiptService service, CancellationToken token) =>
        {
            var result = await service.ListForOrderAsync(purchaseOrderId, token);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        }).WithName("ListGoodsReceiptsForPurchaseOrder").Produces<IReadOnlyList<GoodsReceiptResponse>>(200)
          .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);

        group.MapPost("/purchase-orders/{purchaseOrderId:guid}/receipts", async (Guid purchaseOrderId, CreateGoodsReceiptRequest request,
            HttpRequest httpRequest, IGoodsReceiptService service, CancellationToken token) =>
        {
            var result = await service.CreateAsync(purchaseOrderId, request, httpRequest.Headers["Idempotency-Key"].FirstOrDefault(), token);
            if (!result.IsSuccess) return Failure(result.Error, result.Message);
            return result.WasReplay ? Results.Ok(result.Value) : Results.Created($"/api/v1/goods-receipts/{result.Value!.Id}", result.Value);
        }).WithName("CreateGoodsReceipt").Produces<GoodsReceiptResponse>(201).Produces<GoodsReceiptResponse>(200)
          .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);

        group.MapGet("/goods-receipts/{receiptId:guid}", async (Guid receiptId, IGoodsReceiptService service, CancellationToken token) =>
        {
            var result = await service.GetAsync(receiptId, token);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        }).WithName("GetGoodsReceipt").Produces<GoodsReceiptResponse>(200).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        return endpoints;
    }

    private static IResult Failure(GoodsReceiptError error, string? message)
    {
        var status = error switch
        {
            GoodsReceiptError.Unauthenticated => 401,
            GoodsReceiptError.Forbidden => 403,
            GoodsReceiptError.NotFound => 404,
            GoodsReceiptError.Conflict => 409,
            _ => 400
        };
        return Results.Problem(statusCode: status, title: status switch { 401 => "Authentication required", 403 => "Permission denied", 404 => "Resource not found", 409 => "Goods receipt conflict", _ => "Invalid request" }, detail: message);
    }
}
