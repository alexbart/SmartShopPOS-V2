using Microsoft.EntityFrameworkCore;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Application.Suppliers;
using SmartShopPOS.Contracts.Suppliers;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Persistence;
using SmartShopPOS.Infrastructure.Suppliers;

namespace SmartShopPOS.IntegrationTests;

[Collection("PostgreSQL integration")]
public sealed class SupplierPersistenceTests
{
    [PostgresFact]
    public async Task Suppliers_AreTenantScopedUniqueAndLogicallyDeactivated()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")!;
        var options = new DbContextOptionsBuilder<SmartShopPosDbContext>().UseNpgsql(connectionString).Options;
        var organizationA = new Organization("Supplier Org A", $"supplier-a-{Guid.NewGuid():N}");
        var organizationB = new Organization("Supplier Org B", $"supplier-b-{Guid.NewGuid():N}");
        await using (var setup = new SmartShopPosDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.Organizations.AddRange(organizationA, organizationB);
            await setup.SaveChangesAsync();
        }

        try
        {
            await using var context = new SmartShopPosDbContext(options);
            var userId = Guid.NewGuid();
            var permissions = new TestPermissionChecker("suppliers.view", "suppliers.create", "suppliers.update", "suppliers.deactivate");
            var serviceA = new SupplierService(context, new TestCurrentUser(organizationA.Id, userId, true), permissions);
            var serviceB = new SupplierService(context, new TestCurrentUser(organizationB.Id, userId, true), permissions);

            var created = await serviceA.CreateAsync(Request(" sup-001 ", "Alpha Supplier"));
            Assert.True(created.IsSuccess, created.Message);
            Assert.Equal("SUP-001", created.Value!.Code);
            Assert.True(created.Value.IsActive);
            Assert.Equal(organizationA.Id, await context.Suppliers.Where(s => s.Id == created.Value.Id)
                .Select(s => s.OrganizationId).SingleAsync());

            var sameCodeOtherOrg = await serviceB.CreateAsync(Request("SUP-001", "Other Tenant Supplier"));
            Assert.True(sameCodeOtherOrg.IsSuccess, sameCodeOtherOrg.Message);

            var duplicate = await serviceA.CreateAsync(Request("SUP-001", "Duplicate"));
            Assert.Equal(SupplierError.Conflict, duplicate.Error);

            var listA = await serviceA.ListAsync(new SupplierFilter());
            Assert.Equal(new[] { created.Value.Id }, listA.Value!.Select(s => s.Id));
            Assert.Equal(SupplierError.NotFound, (await serviceA.GetAsync(sameCodeOtherOrg.Value!.Id)).Error);
            Assert.Equal(SupplierError.NotFound,
                (await serviceA.UpdateAsync(sameCodeOtherOrg.Value.Id, Request("SUP-002", "Cross Tenant Update"))).Error);
            Assert.Equal(SupplierError.NotFound, (await serviceA.DeactivateAsync(sameCodeOtherOrg.Value.Id)).Error);

            var updated = await serviceA.UpdateAsync(created.Value.Id, Request("sup-003", "Renamed Supplier"));
            Assert.True(updated.IsSuccess);
            Assert.Equal(organizationA.Id, await context.Suppliers.Where(s => s.Id == created.Value.Id)
                .Select(s => s.OrganizationId).SingleAsync());

            Assert.True((await serviceA.DeactivateAsync(created.Value.Id)).IsSuccess);
            var updatedWhileInactive = await serviceA.UpdateAsync(created.Value.Id, Request("SUP-004", "Updated Inactive Supplier"));
            Assert.True(updatedWhileInactive.IsSuccess, updatedWhileInactive.Message);
            Assert.False(updatedWhileInactive.Value!.IsActive);
            Assert.DoesNotContain(created.Value.Id, (await serviceA.ListAsync(new SupplierFilter(true))).Value!.Select(s => s.Id));
            var persisted = await context.Suppliers.SingleAsync(s => s.Id == created.Value.Id);
            Assert.False(persisted.IsActive);

            var anonymousService = new SupplierService(context, new TestCurrentUser(organizationA.Id, userId, false), permissions);
            Assert.Equal(SupplierError.Unauthenticated, (await anonymousService.ListAsync(new SupplierFilter())).Error);

            var noPermissions = new SupplierService(context, new TestCurrentUser(organizationA.Id, userId, true), new TestPermissionChecker());
            Assert.Equal(SupplierError.Forbidden, (await noPermissions.ListAsync(new SupplierFilter())).Error);
            Assert.Equal(SupplierError.Forbidden, (await noPermissions.CreateAsync(Request("SUP-004", "Denied"))).Error);
            Assert.Equal(SupplierError.Forbidden, (await noPermissions.UpdateAsync(created.Value.Id, Request("SUP-005", "Denied"))).Error);
            Assert.Equal(SupplierError.Forbidden, (await noPermissions.DeactivateAsync(created.Value.Id)).Error);

            context.ChangeTracker.Clear();
            var duplicateEntity = new Supplier(organizationA.Id, "SUP-004", "DB duplicate");
            context.Suppliers.Add(duplicateEntity);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();

            context.Suppliers.Add(new Supplier(Guid.NewGuid(), "SUP-999", "Invalid tenant reference"));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();
        }
        finally
        {
            await using var cleanup = new SmartShopPosDbContext(options);
            var organizationIds = new[] { organizationA.Id, organizationB.Id };
            cleanup.Suppliers.RemoveRange(await cleanup.Suppliers.Where(s => organizationIds.Contains(s.OrganizationId)).ToListAsync());
            cleanup.Organizations.RemoveRange(await cleanup.Organizations.Where(o => organizationIds.Contains(o.Id)).ToListAsync());
            await cleanup.SaveChangesAsync();
        }
    }

    private static SupplierUpsertRequest Request(string code, string name) =>
        new(code, name, null, "Contact Person", "+254 700 000 000", "contact@example.test", null, "TAX-1", "BR-1");

    private sealed class TestCurrentUser(Guid organizationId, Guid userId, bool isAuthenticated) : ICurrentUser
    {
        public bool IsAuthenticated { get; } = isAuthenticated;
        public Guid? UserId { get; } = userId;
        public Guid? OrganizationId { get; } = organizationId;
        public Guid? SessionId => null;
        public Guid? SelectedBranchId => null;
        public string? Email => null;
    }

    private sealed class TestPermissionChecker(params string[] permissions) : IPermissionChecker
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(permissions.Contains(permissionKey, StringComparer.Ordinal));
    }
}
