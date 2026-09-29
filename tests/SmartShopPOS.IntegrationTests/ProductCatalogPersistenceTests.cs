using Microsoft.EntityFrameworkCore;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.IntegrationTests;

[Collection("PostgreSQL integration")]
public sealed class ProductCatalogPersistenceTests
{
    [PostgresFact]
    public async Task ProductCatalog_EnforcesOrganizationScopeAndUniqueConstraints()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var organizationA = new Organization("Catalog Org A", $"catalog-a-{Guid.NewGuid():N}");
        var organizationB = new Organization("Catalog Org B", $"catalog-b-{Guid.NewGuid():N}");
        var categoryA = new Category(organizationA.Id, "Beverages");
        var brandA = new Brand(organizationA.Id, "Acme");
        var unitA = new UnitOfMeasure(organizationA.Id, "KG", "Kilogram", "kg");
        var taxA = new TaxCategory(organizationA.Id, "VAT", "VAT", 16m);

        var categoryB = new Category(organizationB.Id, "Beverages");
        var brandB = new Brand(organizationB.Id, "Acme");
        var unitB = new UnitOfMeasure(organizationB.Id, "KG", "Kilogram", "kg");
        var taxB = new TaxCategory(organizationB.Id, "VAT", "VAT", 16m);

        var options = new DbContextOptionsBuilder<SmartShopPosDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using (var setupContext = new SmartShopPosDbContext(options))
        {
            await setupContext.Database.MigrateAsync();
            setupContext.AddRange(organizationA, organizationB, categoryA, categoryB, brandA, brandB, unitA, unitB, taxA, taxB);
            await setupContext.SaveChangesAsync();
        }

        try
        {
            await using (var context = new SmartShopPosDbContext(options))
            {
                var productA = new Product(
                    organizationA.Id,
                    "tea-001",
                    "ABC123",
                    "Kenyan Tea",
                    "Premium tea",
                    categoryA.Id,
                    brandA.Id,
                    unitA.Id,
                    taxA.Id);

                context.Products.Add(productA);
                await context.SaveChangesAsync();

                var duplicateSku = new Product(
                    organizationA.Id,
                    "TEA-001",
                    "XYZ789",
                    "Duplicate Tea",
                    "Another tea",
                    categoryA.Id,
                    brandA.Id,
                    unitA.Id,
                    taxA.Id);

                context.Products.Add(duplicateSku);
                await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
                context.ChangeTracker.Clear();

                var productB = new Product(
                    organizationB.Id,
                    "tea-001",
                    "ABC123",
                    "Tea in another organization",
                    "Same barcode and SKU in different org",
                    categoryB.Id,
                    brandB.Id,
                    unitB.Id,
                    taxB.Id);

                context.Products.Add(productB);
                await context.SaveChangesAsync();

                var persistedA = await context.Products.SingleAsync(product => product.OrganizationId == organizationA.Id && product.Sku == "TEA-001");
                var persistedB = await context.Products.SingleAsync(product => product.OrganizationId == organizationB.Id && product.Sku == "TEA-001");

                Assert.Equal(organizationA.Id, persistedA.OrganizationId);
                Assert.Equal(organizationB.Id, persistedB.OrganizationId);
                Assert.Equal("ABC123", persistedA.Barcode);
                Assert.Equal("TEA-001", persistedA.Sku);
                Assert.Equal("TEA-001", persistedB.Sku);
            }
        }
        finally
        {
            await using var cleanupContext = new SmartShopPosDbContext(options);
            var orgIds = new[] { organizationA.Id, organizationB.Id };
            cleanupContext.Products.RemoveRange(await cleanupContext.Products.Where(product => orgIds.Contains(product.OrganizationId)).ToListAsync());
            cleanupContext.Categories.RemoveRange(await cleanupContext.Categories.Where(category => orgIds.Contains(category.OrganizationId)).ToListAsync());
            cleanupContext.Brands.RemoveRange(await cleanupContext.Brands.Where(brand => orgIds.Contains(brand.OrganizationId)).ToListAsync());
            cleanupContext.UnitsOfMeasure.RemoveRange(await cleanupContext.UnitsOfMeasure.Where(unit => orgIds.Contains(unit.OrganizationId)).ToListAsync());
            cleanupContext.TaxCategories.RemoveRange(await cleanupContext.TaxCategories.Where(taxCategory => orgIds.Contains(taxCategory.OrganizationId)).ToListAsync());
            await cleanupContext.SaveChangesAsync();
        }
    }
}
