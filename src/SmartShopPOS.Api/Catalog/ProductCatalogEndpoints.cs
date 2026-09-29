using SmartShopPOS.Application.Catalog;
using SmartShopPOS.Contracts.Catalog;

namespace SmartShopPOS.Api.Catalog;

public static class ProductCatalogEndpoints
{
    public static IEndpointRouteBuilder MapProductCatalog(this IEndpointRouteBuilder endpoints)
    {
        var catalog = endpoints.MapGroup("/api/v1").RequireAuthorization();

        var products = catalog.MapGroup("/products").WithTags("Products");
        products.MapGet("", async (
            bool? active,
            string? sku,
            string? barcode,
            string? search,
            Guid? categoryId,
            Guid? brandId,
            IProductCatalogService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListProductsAsync(
                new ProductFilter(active, sku, barcode, search, categoryId, brandId), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("ListProductsV1")
        .Produces<IReadOnlyList<ProductResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        products.MapGet("/{productId:guid}", async (
            Guid productId,
            IProductCatalogService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetProductAsync(productId, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("GetProductV1")
        .Produces<ProductResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        products.MapPost("", async (
            ProductUpsertRequest request,
            IProductCatalogService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateProductAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/products/{result.Value!.Id}", result.Value)
                : Failure(result.Error, result.Message);
        })
        .WithName("CreateProductV1")
        .Produces<ProductResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        products.MapPut("/{productId:guid}", async (
            Guid productId,
            ProductUpsertRequest request,
            IProductCatalogService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateProductAsync(productId, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("UpdateProductV1")
        .Produces<ProductResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        products.MapDelete("/{productId:guid}", async (
            Guid productId,
            IProductCatalogService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.DeactivateProductAsync(productId, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : Failure(result.Error, result.Message);
        })
        .WithName("DeactivateProductV1")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        products.MapGet("/{productId:guid}/price", async (
            Guid productId,
            DateTimeOffset? at,
            IProductPricingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetCurrentPriceAsync(productId, at, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("GetProductPriceV1")
        .Produces<ProductPriceResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        products.MapGet("/{productId:guid}/prices", async (
            Guid productId,
            DateTimeOffset? at,
            IProductPricingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListPricesAsync(productId, at, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("ListProductPricesV1")
        .Produces<IReadOnlyList<ProductPriceResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        products.MapPost("/{productId:guid}/prices", async (
            Guid productId,
            ProductPriceUpsertRequest request,
            IProductPricingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreatePriceAsync(productId, request, cancellationToken);
            return result.IsSuccess ? Results.Created($"/api/v1/products/{productId}/prices/{result.Value!.Id}", result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("CreateProductPriceV1")
        .Produces<ProductPriceResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        var categories = catalog.MapGroup("/categories").WithTags("Categories");
        categories.MapGet("", async (bool? active, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ListCategoriesAsync(active, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("ListCategoriesV1")
        .Produces<IReadOnlyList<CategoryResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
        categories.MapPost("", async (CategoryUpsertRequest request, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateCategoryAsync(request, cancellationToken);
            return result.IsSuccess ? Results.Created($"/api/v1/categories/{result.Value!.Id}", result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("CreateCategoryV1")
        .Produces<CategoryResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);
        categories.MapPut("/{categoryId:guid}", async (Guid categoryId, CategoryUpsertRequest request, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateCategoryAsync(categoryId, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("UpdateCategoryV1")
        .Produces<CategoryResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
        categories.MapDelete("/{categoryId:guid}", async (Guid categoryId, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeactivateCategoryAsync(categoryId, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : Failure(result.Error, result.Message);
        })
        .WithName("DeactivateCategoryV1")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        var brands = catalog.MapGroup("/brands").WithTags("Brands");
        brands.MapGet("", async (bool? active, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ListBrandsAsync(active, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("ListBrandsV1")
        .Produces<IReadOnlyList<BrandResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
        brands.MapPost("", async (BrandUpsertRequest request, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateBrandAsync(request, cancellationToken);
            return result.IsSuccess ? Results.Created($"/api/v1/brands/{result.Value!.Id}", result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("CreateBrandV1")
        .Produces<BrandResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);
        brands.MapPut("/{brandId:guid}", async (Guid brandId, BrandUpsertRequest request, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateBrandAsync(brandId, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("UpdateBrandV1")
        .Produces<BrandResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
        brands.MapDelete("/{brandId:guid}", async (Guid brandId, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeactivateBrandAsync(brandId, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : Failure(result.Error, result.Message);
        })
        .WithName("DeactivateBrandV1")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        var units = catalog.MapGroup("/units").WithTags("Units of measure");
        units.MapGet("", async (bool? active, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ListUnitsAsync(active, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("ListUnitsV1")
        .Produces<IReadOnlyList<UnitOfMeasureResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
        units.MapPost("", async (UnitOfMeasureUpsertRequest request, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateUnitAsync(request, cancellationToken);
            return result.IsSuccess ? Results.Created($"/api/v1/units/{result.Value!.Id}", result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("CreateUnitV1")
        .Produces<UnitOfMeasureResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);
        units.MapPut("/{unitId:guid}", async (Guid unitId, UnitOfMeasureUpsertRequest request, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateUnitAsync(unitId, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("UpdateUnitV1")
        .Produces<UnitOfMeasureResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
        units.MapDelete("/{unitId:guid}", async (Guid unitId, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeactivateUnitAsync(unitId, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : Failure(result.Error, result.Message);
        })
        .WithName("DeactivateUnitV1")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        var taxCategories = catalog.MapGroup("/tax-categories").WithTags("Tax categories");
        taxCategories.MapGet("", async (bool? active, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ListTaxCategoriesAsync(active, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("ListTaxCategoriesV1")
        .Produces<IReadOnlyList<TaxCategoryResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
        taxCategories.MapPost("", async (TaxCategoryUpsertRequest request, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateTaxCategoryAsync(request, cancellationToken);
            return result.IsSuccess ? Results.Created($"/api/v1/tax-categories/{result.Value!.Id}", result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("CreateTaxCategoryV1")
        .Produces<TaxCategoryResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);
        taxCategories.MapPut("/{taxCategoryId:guid}", async (Guid taxCategoryId, TaxCategoryUpsertRequest request, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateTaxCategoryAsync(taxCategoryId, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Failure(result.Error, result.Message);
        })
        .WithName("UpdateTaxCategoryV1")
        .Produces<TaxCategoryResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
        taxCategories.MapDelete("/{taxCategoryId:guid}", async (Guid taxCategoryId, IProductCatalogService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeactivateTaxCategoryAsync(taxCategoryId, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : Failure(result.Error, result.Message);
        })
        .WithName("DeactivateTaxCategoryV1")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static IResult Failure(CatalogError error, string? message)
    {
        var (statusCode, title) = error switch
        {
            CatalogError.Unauthenticated => (StatusCodes.Status401Unauthorized, "Authentication required"),
            CatalogError.Forbidden => (StatusCodes.Status403Forbidden, "Permission denied"),
            CatalogError.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
            CatalogError.Conflict => (StatusCodes.Status409Conflict, "Resource conflict"),
            _ => (StatusCodes.Status400BadRequest, "Invalid request")
        };

        return Results.Problem(statusCode: statusCode, title: title, detail: message);
    }
}