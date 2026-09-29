using SmartShopPOS.Application.Suppliers;
using SmartShopPOS.Contracts.Suppliers;

namespace SmartShopPOS.Api.Suppliers;

public static class SupplierEndpoints
{
    public static IEndpointRouteBuilder MapSuppliers(this IEndpointRouteBuilder endpoints)
    {
        var suppliers = endpoints.MapGroup("/api/v1/suppliers").RequireAuthorization().WithTags("Suppliers");

        suppliers.MapGet("", async (bool? active, ISupplierService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ListAsync(new SupplierFilter(active), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("ListSuppliersV1")
        .Produces<IReadOnlyList<SupplierResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        suppliers.MapPost("", async (SupplierUpsertRequest request, ISupplierService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/suppliers/{result.Value!.Id}", result.Value)
                : Failure(result.Error, result.Message);
        })
        .WithName("CreateSupplierV1")
        .Produces<SupplierResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);

        suppliers.MapGet("/{supplierId:guid}", async (Guid supplierId, ISupplierService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(supplierId, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("GetSupplierV1")
        .Produces<SupplierResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        suppliers.MapPut("/{supplierId:guid}", async (Guid supplierId, SupplierUpsertRequest request, ISupplierService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(supplierId, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("UpdateSupplierV1")
        .Produces<SupplierResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        suppliers.MapDelete("/{supplierId:guid}", async (Guid supplierId, ISupplierService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeactivateAsync(supplierId, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : Failure(result.Error, result.Message);
        })
        .WithName("DeactivateSupplierV1")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static IResult Failure(SupplierError error, string? message)
    {
        var (statusCode, title) = error switch
        {
            SupplierError.Unauthenticated => (StatusCodes.Status401Unauthorized, "Authentication required"),
            SupplierError.Forbidden => (StatusCodes.Status403Forbidden, "Permission denied"),
            SupplierError.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
            SupplierError.Conflict => (StatusCodes.Status409Conflict, "Resource conflict"),
            _ => (StatusCodes.Status400BadRequest, "Invalid request")
        };
        return Results.Problem(statusCode: statusCode, title: title, detail: message);
    }
}
