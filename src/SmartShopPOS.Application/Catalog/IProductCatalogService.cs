using SmartShopPOS.Contracts.Catalog;

namespace SmartShopPOS.Application.Catalog;

public interface IProductCatalogService
{
    Task<CatalogResult<IReadOnlyList<ProductResponse>>> ListProductsAsync(ProductFilter filter, CancellationToken cancellationToken = default);
    Task<CatalogResult<ProductResponse>> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<CatalogResult<ProductResponse>> CreateProductAsync(ProductUpsertRequest request, CancellationToken cancellationToken = default);
    Task<CatalogResult<ProductResponse>> UpdateProductAsync(Guid productId, ProductUpsertRequest request, CancellationToken cancellationToken = default);
    Task<CatalogResult<bool>> DeactivateProductAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<CatalogResult<IReadOnlyList<CategoryResponse>>> ListCategoriesAsync(bool? active, CancellationToken cancellationToken = default);
    Task<CatalogResult<CategoryResponse>> CreateCategoryAsync(CategoryUpsertRequest request, CancellationToken cancellationToken = default);
    Task<CatalogResult<CategoryResponse>> UpdateCategoryAsync(Guid categoryId, CategoryUpsertRequest request, CancellationToken cancellationToken = default);
    Task<CatalogResult<bool>> DeactivateCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task<CatalogResult<IReadOnlyList<BrandResponse>>> ListBrandsAsync(bool? active, CancellationToken cancellationToken = default);
    Task<CatalogResult<BrandResponse>> CreateBrandAsync(BrandUpsertRequest request, CancellationToken cancellationToken = default);
    Task<CatalogResult<BrandResponse>> UpdateBrandAsync(Guid brandId, BrandUpsertRequest request, CancellationToken cancellationToken = default);
    Task<CatalogResult<bool>> DeactivateBrandAsync(Guid brandId, CancellationToken cancellationToken = default);

    Task<CatalogResult<IReadOnlyList<UnitOfMeasureResponse>>> ListUnitsAsync(bool? active, CancellationToken cancellationToken = default);
    Task<CatalogResult<UnitOfMeasureResponse>> CreateUnitAsync(UnitOfMeasureUpsertRequest request, CancellationToken cancellationToken = default);
    Task<CatalogResult<UnitOfMeasureResponse>> UpdateUnitAsync(Guid unitId, UnitOfMeasureUpsertRequest request, CancellationToken cancellationToken = default);
    Task<CatalogResult<bool>> DeactivateUnitAsync(Guid unitId, CancellationToken cancellationToken = default);

    Task<CatalogResult<IReadOnlyList<TaxCategoryResponse>>> ListTaxCategoriesAsync(bool? active, CancellationToken cancellationToken = default);
    Task<CatalogResult<TaxCategoryResponse>> CreateTaxCategoryAsync(TaxCategoryUpsertRequest request, CancellationToken cancellationToken = default);
    Task<CatalogResult<TaxCategoryResponse>> UpdateTaxCategoryAsync(Guid taxCategoryId, TaxCategoryUpsertRequest request, CancellationToken cancellationToken = default);
    Task<CatalogResult<bool>> DeactivateTaxCategoryAsync(Guid taxCategoryId, CancellationToken cancellationToken = default);
}