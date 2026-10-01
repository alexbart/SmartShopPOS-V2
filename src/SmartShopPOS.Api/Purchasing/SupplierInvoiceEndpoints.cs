using SmartShopPOS.Application.Purchasing;
using SmartShopPOS.Contracts.Purchasing;

namespace SmartShopPOS.Api.Purchasing;

public static class SupplierInvoiceEndpoints
{
    public static IEndpointRouteBuilder MapSupplierInvoices(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/supplier-invoices").RequireAuthorization().WithTags("Supplier invoices");
        group.MapGet("", async (ISupplierInvoiceService service, CancellationToken token) =>
        {
            var result = await service.ListAsync(token);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        }).WithName("ListSupplierInvoices").Produces<IReadOnlyList<SupplierInvoiceSummaryResponse>>(200).ProducesProblem(401).ProducesProblem(403);
        group.MapPost("", async (SupplierInvoiceCreateRequest request, ISupplierInvoiceService service, CancellationToken token) =>
        {
            var result = await service.CreateAsync(request, token);
            return result.IsSuccess ? Results.Created($"/api/v1/supplier-invoices/{result.Value!.Id}", result.Value) : Failure(result.Error, result.Message);
        }).WithName("CreateSupplierInvoice").Produces<SupplierInvoiceResponse>(201).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/{id:guid}", async (Guid id, ISupplierInvoiceService service, CancellationToken token) =>
        {
            var result = await service.GetAsync(id, token); return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        }).WithName("GetSupplierInvoice").Produces<SupplierInvoiceResponse>(200).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        group.MapPut("/{id:guid}", async (Guid id, SupplierInvoiceUpdateRequest request, ISupplierInvoiceService service, CancellationToken token) =>
        {
            var result = await service.UpdateAsync(id, request, token); return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        }).WithName("UpdateSupplierInvoice").Produces<SupplierInvoiceResponse>(200).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/{id:guid}/lines", async (Guid id, SupplierInvoiceLineRequest request, ISupplierInvoiceService service, CancellationToken token) =>
        {
            var result = await service.AddLineAsync(id, request, token);
            return result.IsSuccess ? Results.Created($"/api/v1/supplier-invoices/{id}/lines/{result.Value!.Id}", result.Value) : Failure(result.Error, result.Message);
        }).WithName("AddSupplierInvoiceLine").Produces<SupplierInvoiceLineResponse>(201).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPut("/{id:guid}/lines/{lineId:guid}", async (Guid id, Guid lineId, SupplierInvoiceLineRequest request, ISupplierInvoiceService service, CancellationToken token) =>
        {
            var result = await service.UpdateLineAsync(id, lineId, request, token); return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        }).WithName("UpdateSupplierInvoiceLine").Produces<SupplierInvoiceLineResponse>(200).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapDelete("/{id:guid}/lines/{lineId:guid}", async (Guid id, Guid lineId, ISupplierInvoiceService service, CancellationToken token) =>
        {
            var result = await service.DeleteLineAsync(id, lineId, token); return result.IsSuccess ? Results.NoContent() : Failure(result.Error, result.Message);
        }).WithName("DeleteSupplierInvoiceLine").Produces(204).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/{id:guid}/post", async (Guid id, ISupplierInvoiceService service, CancellationToken token) =>
        {
            var result = await service.PostAsync(id, token); return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        }).WithName("PostSupplierInvoice").Produces<SupplierInvoiceResponse>(200).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/{id:guid}/cancel", async (Guid id, ISupplierInvoiceService service, CancellationToken token) =>
        {
            var result = await service.CancelAsync(id, token); return result.IsSuccess ? Results.NoContent() : Failure(result.Error, result.Message);
        }).WithName("CancelSupplierInvoice").Produces(204).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        return endpoints;
    }

    private static IResult Failure(SupplierInvoiceError error, string? message)
    {
        var status = error switch
        {
            SupplierInvoiceError.Unauthenticated => 401,
            SupplierInvoiceError.Forbidden => 403,
            SupplierInvoiceError.NotFound => 404,
            SupplierInvoiceError.Conflict => 409,
            _ => 400
        };
        var title = status switch { 401 => "Authentication required", 403 => "Permission denied", 404 => "Resource not found", 409 => "Supplier invoice conflict", _ => "Invalid request" };
        return Results.Problem(statusCode: status, title: title, detail: message);
    }
}
