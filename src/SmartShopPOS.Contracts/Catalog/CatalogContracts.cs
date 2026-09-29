namespace SmartShopPOS.Contracts.Catalog;

public sealed record ProductFilter(
    bool? Active = null,
    string? Sku = null,
    string? Barcode = null,
    string? Search = null,
    Guid? CategoryId = null,
    Guid? BrandId = null);

public sealed record ProductUpsertRequest(
    string Sku,
    string? Barcode,
    string Name,
    string? Description,
    Guid? CategoryId,
    Guid? BrandId,
    Guid UnitOfMeasureId,
    Guid TaxCategoryId);

public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string? Barcode,
    string Name,
    string? Description,
    Guid? CategoryId,
    Guid? BrandId,
    Guid UnitOfMeasureId,
    Guid TaxCategoryId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CategoryUpsertRequest(string Name, string? Description);

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record BrandUpsertRequest(string Name, string? Description);

public sealed record BrandResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UnitOfMeasureUpsertRequest(string Code, string Name, string? Symbol);

public sealed record UnitOfMeasureResponse(
    Guid Id,
    string Code,
    string Name,
    string? Symbol,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TaxCategoryUpsertRequest(string Code, string Name, decimal Rate);

public sealed record TaxCategoryResponse(
    Guid Id,
    string Code,
    string Name,
    decimal Rate,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProductPriceUpsertRequest(decimal CostPrice, decimal SellingPrice, DateTimeOffset EffectiveFrom);

public sealed record ProductPriceResponse(
    Guid Id,
    Guid ProductId,
    decimal CostPrice,
    decimal SellingPrice,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);