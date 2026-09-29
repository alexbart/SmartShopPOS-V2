using Microsoft.EntityFrameworkCore;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Application.UserBranches;
using SmartShopPOS.Contracts.Inventory;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Inventory;
using SmartShopPOS.Infrastructure.Persistence;
using SmartShopPOS.Infrastructure.UserBranches;

namespace SmartShopPOS.IntegrationTests;

[Collection("PostgreSQL integration")]
public sealed class InventoryPersistenceTests
{
    [PostgresFact]
    public async Task Inventory_OpeningAndAdjustmentsKeepLedgerAndBalanceAtomic()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")!;
        var options = new DbContextOptionsBuilder<SmartShopPosDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var organization = new Organization("Inventory Service Org", $"inventory-service-{Guid.NewGuid():N}");
        var user = new User(organization.Id, $"user-{Guid.NewGuid():N}@example.test", "Inventory User", "test-hash");
        var branch = new Branch(organization.Id, "MAIN", "Main Branch");
        var category = new Category(organization.Id, "General");
        var brand = new Brand(organization.Id, "House");
        var unit = new UnitOfMeasure(organization.Id, "KG", "Kilogram", "kg");
        var tax = new TaxCategory(organization.Id, "VAT", "VAT", 16m);
        var product = new Product(organization.Id, "RICE", null, "Rice", null,
            category.Id, brand.Id, unit.Id, tax.Id);
        var sessionId = Guid.NewGuid();
        var session = new AuthenticationSession(user.Id, organization.Id, sessionId, $"hash-{Guid.NewGuid():N}");
        session.SetSelectedBranch(branch.Id);
        var assignment = new UserBranch(organization.Id, user.Id, branch.Id);

        await using (var setup = new SmartShopPosDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.AddRange(organization, user, branch, category, brand, unit, tax, product, session, assignment);
            await setup.SaveChangesAsync();
        }

        await using (var context = new SmartShopPosDbContext(options))
        {
            var currentUser = new TestCurrentUser(organization.Id, user.Id, sessionId, branch.Id);
            var permissions = new AllowInventoryPermissionChecker();
            IBranchAccessService branchAccess = new BranchAccessService(context, currentUser, permissions);
            var service = new InventoryService(context, currentUser, permissions, branchAccess);

            var opening = await service.CreateOpeningBalanceAsync(product.Id, new OpeningBalanceRequest(10m, "Initial count"));
            Assert.True(opening.IsSuccess, opening.Message);
            Assert.Equal(10m, opening.Value!.QuantityOnHand);

            var duplicateOpening = await service.CreateOpeningBalanceAsync(product.Id, new OpeningBalanceRequest(2m, "Second baseline"));
            Assert.Equal(InventoryError.Conflict, duplicateOpening.Error);

            var insufficient = await service.CreateAdjustmentDecreaseAsync(product.Id, new AdjustmentRequest(15m, "Count correction"));
            Assert.Equal(InventoryError.InsufficientStock, insufficient.Error);

            var movements = await context.StockMovements.CountAsync(m => m.OrganizationId == organization.Id && m.ProductId == product.Id);
            Assert.Equal(1, movements);
            Assert.Equal(10m, await context.InventoryBalances.Where(b => b.ProductId == product.Id)
                .Select(b => b.QuantityOnHand).SingleAsync());
        }
    }

    [PostgresFact]
    public async Task Inventory_EnforcesTenantOwnershipAndOneBalancePerBranchProduct()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")!;
        var options = new DbContextOptionsBuilder<SmartShopPosDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var organizationA = new Organization("Inventory Org A", $"inventory-a-{Guid.NewGuid():N}");
        var organizationB = new Organization("Inventory Org B", $"inventory-b-{Guid.NewGuid():N}");
        var branchA = new Branch(organizationA.Id, "NAIROBI", "Nairobi");
        var branchA2 = new Branch(organizationA.Id, "ELDORET", "Eldoret");
        var branchB = new Branch(organizationB.Id, "NAIROBI", "Nairobi");
        var categoryA = new Category(organizationA.Id, "General");
        var categoryB = new Category(organizationB.Id, "General");
        var brandA = new Brand(organizationA.Id, "House");
        var brandB = new Brand(organizationB.Id, "House");
        var unitA = new UnitOfMeasure(organizationA.Id, "KG", "Kilogram", "kg");
        var unitB = new UnitOfMeasure(organizationB.Id, "KG", "Kilogram", "kg");
        var taxA = new TaxCategory(organizationA.Id, "VAT", "VAT", 16m);
        var taxB = new TaxCategory(organizationB.Id, "VAT", "VAT", 16m);

        await using (var setup = new SmartShopPosDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.AddRange(organizationA, organizationB, branchA, branchA2, branchB,
                categoryA, categoryB, brandA, brandB, unitA, unitB, taxA, taxB);
            await setup.SaveChangesAsync();

            var productA = new Product(organizationA.Id, "FLOUR", null, "Flour", null,
                categoryA.Id, brandA.Id, unitA.Id, taxA.Id);
            var productB = new Product(organizationB.Id, "FLOUR", null, "Flour", null,
                categoryB.Id, brandB.Id, unitB.Id, taxB.Id);
            setup.Products.AddRange(productA, productB);
            await setup.SaveChangesAsync();

            setup.InventoryBalances.AddRange(
                new InventoryBalance(organizationA.Id, branchA.Id, productA.Id, 2.5m),
                new InventoryBalance(organizationA.Id, branchA2.Id, productA.Id, 0.75m));
            await setup.SaveChangesAsync();

            Assert.Equal(2, await setup.InventoryBalances.CountAsync(b => b.ProductId == productA.Id));
            var branchABalance = await setup.InventoryBalances.SingleAsync(b => b.BranchId == branchA.Id);
            Assert.Equal(2.5m, branchABalance.QuantityOnHand);
        }

        await using (var context = new SmartShopPosDbContext(options))
        {
            var productA = await context.Products.SingleAsync(p => p.OrganizationId == organizationA.Id);
            var productB = await context.Products.SingleAsync(p => p.OrganizationId == organizationB.Id);

            context.InventoryBalances.Add(new InventoryBalance(organizationA.Id, branchA.Id, productA.Id, 1m));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();

            context.InventoryBalances.Add(new InventoryBalance(organizationA.Id, branchB.Id, productA.Id, 1m));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();

            context.InventoryBalances.Add(new InventoryBalance(organizationA.Id, branchA.Id, productB.Id, 1m));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();
        }
    }

    private sealed class TestCurrentUser(Guid organizationId, Guid userId, Guid sessionId, Guid branchId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public Guid? OrganizationId => organizationId;
        public Guid? SessionId => sessionId;
        public Guid? SelectedBranchId => branchId;
        public string? Email => "inventory-user@example.test";
    }

    private sealed class AllowInventoryPermissionChecker : IPermissionChecker
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
