using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.UnitTests;

public sealed class ProductCatalogTests
{
    [Fact]
    public void Product_NormalizesSkuAndBarcodeAndHasNoOperationalQuantityOrPrice()
    {
        var product = new Product(
            Guid.NewGuid(),
            " tea-001 ",
            " abC123 ",
            " Kenyan Tea ",
            "  Black tea  ",
            null,
            null,
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.Equal("TEA-001", product.Sku);
        Assert.Equal("ABC123", product.Barcode);
        Assert.Equal("Kenyan Tea", product.Name);
        Assert.Null(product.CategoryId);
        Assert.Null(product.BrandId);
        Assert.True(product.IsActive);
        Assert.DoesNotContain(typeof(Product).GetProperties(), property =>
            property.Name.Contains("Price", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Quantity", StringComparison.OrdinalIgnoreCase) ||
            property.Name == "BranchId");
    }

    [Fact]
    public void Product_RequiresOrganizationSkuUnitAndTaxCategory()
    {
        Assert.Throws<DomainException>(() => new Product(
            Guid.Empty, "SKU", null, "Name", null, null, null, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new Product(
            Guid.NewGuid(), " ", null, "Name", null, null, null, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new Product(
            Guid.NewGuid(), "SKU", null, "Name", null, null, null, Guid.Empty, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new Product(
            Guid.NewGuid(), "SKU", null, "Name", null, null, null, Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void Product_RejectsEmptyOptionalReferenceIdentifiers()
    {
        Assert.Throws<DomainException>(() => new Product(
            Guid.NewGuid(), "SKU", null, "Name", null, Guid.Empty, null, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new Product(
            Guid.NewGuid(), "SKU", null, "Name", null, null, Guid.Empty, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void CategoryAndBrand_NormalizeNamesAndRetainDeactivatedState()
    {
        var category = new Category(Guid.NewGuid(), "  Beverages  ");
        var brand = new Brand(Guid.NewGuid(), "  Acme  ");

        Assert.Equal("BEVERAGES", category.NormalizedName);
        Assert.Equal("ACME", brand.NormalizedName);

        category.SetActive(false);
        brand.SetActive(false);

        Assert.False(category.IsActive);
        Assert.False(brand.IsActive);
    }

    [Fact]
    public void UnitOfMeasure_NormalizesCodeAndRequiresName()
    {
        var unit = new UnitOfMeasure(Guid.NewGuid(), " kg ", " Kilogram ", " kg ");

        Assert.Equal("KG", unit.Code);
        Assert.Equal("Kilogram", unit.Name);
        Assert.Equal("kg", unit.Symbol);
        Assert.Throws<DomainException>(() => new UnitOfMeasure(Guid.NewGuid(), "KG", " "));
    }

    [Fact]
    public void TaxCategory_UsesDecimalPercentageRateAndValidatesRange()
    {
        var zeroRate = new TaxCategory(Guid.NewGuid(), "zero", "Zero rated", 0m);
        var standardRate = new TaxCategory(Guid.NewGuid(), "standard", "Standard", 100m);

        Assert.Equal("ZERO", zeroRate.Code);
        Assert.Equal(0m, zeroRate.Rate);
        Assert.Equal(100m, standardRate.Rate);
        Assert.Throws<DomainException>(() => new TaxCategory(Guid.NewGuid(), "negative", "Negative", -0.01m));
        Assert.Throws<DomainException>(() => new TaxCategory(Guid.NewGuid(), "over", "Over", 100.01m));
    }
}