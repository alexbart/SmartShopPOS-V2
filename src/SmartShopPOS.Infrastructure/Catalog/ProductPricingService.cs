using Microsoft.EntityFrameworkCore;
using SmartShopPOS.Application.Catalog;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Contracts.Catalog;
using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.Infrastructure.Catalog;

public sealed class ProductPricingService(
    SmartShopPosDbContext dbContext,
    ICurrentUser currentUser,
    IPermissionChecker permissionChecker) : IProductPricingService
{
    public async Task<CatalogResult<IReadOnlyList<ProductPriceResponse>>> ListPricesAsync(
        Guid productId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("products.prices.view", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<IReadOnlyList<ProductPriceResponse>>.Failure(authorization.Value, Message(authorization.Value));
        }

        var organizationId = currentUser.OrganizationId!.Value;
        var product = await dbContext.Products.SingleOrDefaultAsync(
            candidate => candidate.Id == productId && candidate.OrganizationId == organizationId,
            cancellationToken);

        if (product is null)
        {
            return CatalogResult<IReadOnlyList<ProductPriceResponse>>.Failure(CatalogError.NotFound, "Product not found.");
        }

        var query = dbContext.ProductPrices
            .Where(price => price.OrganizationId == organizationId && price.ProductId == productId)
            .AsQueryable();

        if (at is DateTimeOffset effectiveAt)
        {
            query = query.Where(price => price.EffectiveFrom <= effectiveAt && (!price.EffectiveTo.HasValue || effectiveAt < price.EffectiveTo.Value));
        }

        var prices = await query.OrderByDescending(price => price.EffectiveFrom).ToListAsync(cancellationToken);
        return CatalogResult<IReadOnlyList<ProductPriceResponse>>.Success(prices.Select(ToResponse).ToList());
    }

    public async Task<CatalogResult<ProductPriceResponse>> GetCurrentPriceAsync(
        Guid productId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ListPricesAsync(productId, at ?? DateTimeOffset.UtcNow, cancellationToken);
        if (!result.IsSuccess)
        {
            return CatalogResult<ProductPriceResponse>.Failure(result.Error, result.Message ?? "Product price not found.");
        }

        if (result.Value is null || result.Value.Count == 0)
        {
            return CatalogResult<ProductPriceResponse>.Failure(CatalogError.NotFound, "Product price not found.");
        }

        return CatalogResult<ProductPriceResponse>.Success(result.Value[0]);
    }

    public async Task<CatalogResult<ProductPriceResponse>> GetPriceAtAsync(
        Guid productId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        return await GetCurrentPriceAsync(productId, at, cancellationToken);
    }

    public async Task<CatalogResult<ProductPriceResponse>> CreatePriceAsync(
        Guid productId,
        ProductPriceUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("products.prices.create", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<ProductPriceResponse>.Failure(authorization.Value, Message(authorization.Value));
        }

        if (request is null)
        {
            return CatalogResult<ProductPriceResponse>.Failure(CatalogError.Invalid, "A product price request is required.");
        }

        var organizationId = currentUser.OrganizationId!.Value;
        var product = await dbContext.Products.SingleOrDefaultAsync(
            candidate => candidate.Id == productId && candidate.OrganizationId == organizationId,
            cancellationToken);

        if (product is null)
        {
            return CatalogResult<ProductPriceResponse>.Failure(CatalogError.NotFound, "Product not found.");
        }

        if (!product.IsActive)
        {
            return CatalogResult<ProductPriceResponse>.Failure(CatalogError.Conflict, "Inactive products cannot receive new pricing.");
        }

        ProductPrice price;
        try
        {
            price = new ProductPrice(organizationId, productId, request.CostPrice, request.SellingPrice, request.EffectiveFrom);
        }
        catch (DomainException exception)
        {
            return CatalogResult<ProductPriceResponse>.Failure(CatalogError.Invalid, exception.Message);
        }

        var latestPrice = await dbContext.ProductPrices
            .Where(candidate => candidate.OrganizationId == organizationId && candidate.ProductId == productId)
            .OrderByDescending(candidate => candidate.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestPrice is not null)
        {
            if (price.EffectiveFrom <= latestPrice.EffectiveFrom)
            {
                return CatalogResult<ProductPriceResponse>.Failure(CatalogError.Conflict, "A new price must begin after the latest effective-from date.");
            }

            if (latestPrice.EffectiveTo is not null && price.EffectiveFrom < latestPrice.EffectiveTo.Value)
            {
                return CatalogResult<ProductPriceResponse>.Failure(CatalogError.Conflict, "Price periods cannot overlap.");
            }

            if (latestPrice.EffectiveTo is null)
            {
                latestPrice.CloseAt(price.EffectiveFrom);
            }
        }

        dbContext.ProductPrices.Add(price);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            if (IsUniqueViolation(exception))
            {
                return CatalogResult<ProductPriceResponse>.Failure(CatalogError.Conflict, "A currently active product price already exists for this product.");
            }

            throw;
        }

        return CatalogResult<ProductPriceResponse>.Success(ToResponse(price));
    }

    private async Task<CatalogError?> AuthorizeAsync(string permission, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty ||
            currentUser.OrganizationId is not Guid organizationId || organizationId == Guid.Empty)
        {
            return CatalogError.Unauthenticated;
        }

        return await permissionChecker.HasPermissionAsync(userId, permission, cancellationToken)
            ? null
            : CatalogError.Forbidden;
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException?.Message.Contains("ux_product_prices_organization_product_active", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string Message(CatalogError error)
    {
        return error == CatalogError.Unauthenticated
            ? "Authentication is required."
            : "The required pricing permission is missing.";
    }

    private static ProductPriceResponse ToResponse(ProductPrice price)
    {
        return new ProductPriceResponse(
            price.Id,
            price.ProductId,
            price.CostPrice,
            price.SellingPrice,
            price.EffectiveFrom,
            price.EffectiveTo,
            price.CreatedAt,
            price.UpdatedAt);
    }
}
