using SmartShopPOS.Contracts.Catalog;

namespace SmartShopPOS.Application.Catalog;

public interface IProductPricingService
{
    Task<CatalogResult<IReadOnlyList<ProductPriceResponse>>> ListPricesAsync(
        Guid productId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default);

    Task<CatalogResult<ProductPriceResponse>> GetCurrentPriceAsync(
        Guid productId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default);

    Task<CatalogResult<ProductPriceResponse>> GetPriceAtAsync(
        Guid productId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);

    Task<CatalogResult<ProductPriceResponse>> CreatePriceAsync(
        Guid productId,
        ProductPriceUpsertRequest request,
        CancellationToken cancellationToken = default);
}
