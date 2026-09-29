using Microsoft.EntityFrameworkCore;
using SmartShopPOS.Application.Catalog;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Contracts.Catalog;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Catalog;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.IntegrationTests;

[Collection("PostgreSQL integration")]
public sealed class ProductPricingPersistenceTests
{
    [PostgresFact]
    public async Task ProductPricing_TracksHistoryAndEnforcesOrganizationScope()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var organizationA = new Organization("Pricing Org A", $"pricing-a-{Guid.NewGuid():N}");
        var organizationB = new Organization("Pricing Org B", $"pricing-b-{Guid.NewGuid():N}");
        var categoryA = new Category(organizationA.Id, "Beverages");
        var categoryB = new Category(organizationB.Id, "Beverages");
        var brandA = new Brand(organizationA.Id, "Acme");
        var brandB = new Brand(organizationB.Id, "Acme");
        var unitA = new UnitOfMeasure(organizationA.Id, "KG", "Kilogram", "kg");
        var unitB = new UnitOfMeasure(organizationB.Id, "KG", "Kilogram", "kg");
        var taxA = new TaxCategory(organizationA.Id, "VAT", "VAT", 16m);
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
            var productB = new Product(
                organizationB.Id,
                "tea-001",
                "XYZ999",
                "Tea in another org",
                "Premium tea",
                categoryB.Id,
                brandB.Id,
                unitB.Id,
                taxB.Id);

            context.Products.AddRange(productA, productB);
            await context.SaveChangesAsync();

            var pricingServiceA = new ProductPricingService(
                context,
                new TestCurrentUser(organizationA.Id, Guid.NewGuid(), true),
                new TestPermissionChecker());
            var pricingServiceB = new ProductPricingService(
                context,
                new TestCurrentUser(organizationB.Id, Guid.NewGuid(), true),
                new TestPermissionChecker());

            var firstPrice = await pricingServiceA.CreatePriceAsync(
                productA.Id,
                new ProductPriceUpsertRequest(100m, 150m, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
            Assert.True(firstPrice.IsSuccess, firstPrice.Message);
            Assert.Equal(150m, firstPrice.Value!.SellingPrice);

            var secondPrice = await pricingServiceA.CreatePriceAsync(
                productA.Id,
                new ProductPriceUpsertRequest(110m, 170m, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)));
            Assert.True(secondPrice.IsSuccess, secondPrice.Message);

            var currentPrice = await pricingServiceA.GetCurrentPriceAsync(productA.Id, new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero));
            Assert.True(currentPrice.IsSuccess);
            Assert.Equal(170m, currentPrice.Value!.SellingPrice);

            var allPrices = await pricingServiceA.ListPricesAsync(productA.Id);
            Assert.True(allPrices.IsSuccess);
            Assert.Equal(2, allPrices.Value!.Count);

            var crossOrganizationAccess = await pricingServiceA.GetCurrentPriceAsync(productB.Id, DateTimeOffset.UtcNow);
            Assert.Equal(CatalogError.NotFound, crossOrganizationAccess.Error);

            var overlap = await pricingServiceA.CreatePriceAsync(
                productA.Id,
                new ProductPriceUpsertRequest(120m, 190m, new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero)));
            Assert.Equal(CatalogError.Conflict, overlap.Error);

            var inaccessibleForOtherOrg = await pricingServiceB.GetCurrentPriceAsync(productA.Id, DateTimeOffset.UtcNow);
            Assert.Equal(CatalogError.NotFound, inaccessibleForOtherOrg.Error);
        }
    }

    private sealed class TestCurrentUser(Guid organizationId, Guid userId, bool isAuthenticated) : ICurrentUser
    {
        public bool IsAuthenticated { get; } = isAuthenticated;
        public Guid? UserId { get; } = userId;
        public Guid? OrganizationId { get; } = organizationId;
        public Guid? SessionId { get; } = Guid.NewGuid();
        public string? Email { get; } = "pricing.user@example.test";
    }

    private sealed class TestPermissionChecker : IPermissionChecker
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(permissionKey is "products.prices.view" or "products.prices.create");
        }
    }
}
