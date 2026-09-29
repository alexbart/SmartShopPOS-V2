namespace SmartShopPOS.Domain.Identity;

public sealed class Product : Entity
{
    private Product()
    {
    }

    public Product(
        Guid organizationId,
        string sku,
        string? barcode,
        string name,
        string? description,
        Guid? categoryId,
        Guid? brandId,
        Guid unitOfMeasureId,
        Guid taxCategoryId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A product must belong to an organization.");
        }

        OrganizationId = organizationId;
        SetDetails(sku, barcode, name, description, categoryId, brandId, unitOfMeasureId, taxCategoryId);
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid OrganizationId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string? Barcode { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? BrandId { get; private set; }
    public Guid UnitOfMeasureId { get; private set; }
    public Guid TaxCategoryId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Organization Organization { get; private set; } = null!;
    public Category? Category { get; private set; }
    public Brand? Brand { get; private set; }
    public UnitOfMeasure UnitOfMeasure { get; private set; } = null!;
    public TaxCategory TaxCategory { get; private set; } = null!;

    public void Update(
        string sku,
        string? barcode,
        string name,
        string? description,
        Guid? categoryId,
        Guid? brandId,
        Guid unitOfMeasureId,
        Guid taxCategoryId)
    {
        SetDetails(sku, barcode, name, description, categoryId, brandId, unitOfMeasureId, taxCategoryId);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void SetDetails(
        string sku,
        string? barcode,
        string name,
        string? description,
        Guid? categoryId,
        Guid? brandId,
        Guid unitOfMeasureId,
        Guid taxCategoryId)
    {
        if (categoryId == Guid.Empty || brandId == Guid.Empty || unitOfMeasureId == Guid.Empty || taxCategoryId == Guid.Empty)
        {
            throw new DomainException("Product references must use valid identifiers.");
        }

        Sku = Organization.Required(sku, nameof(sku), 64).ToUpperInvariant();
        Barcode = CatalogValidation.OptionalValue(barcode, nameof(barcode), 128)?.ToUpperInvariant();
        Name = Organization.Required(name, nameof(name), 200);
        Description = CatalogValidation.OptionalDescription(description);
        CategoryId = categoryId;
        BrandId = brandId;
        UnitOfMeasureId = RequireId(unitOfMeasureId, nameof(unitOfMeasureId));
        TaxCategoryId = RequireId(taxCategoryId, nameof(taxCategoryId));
    }

    private static Guid RequireId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException($"{parameterName} is required.");
        }

        return id;
    }
}