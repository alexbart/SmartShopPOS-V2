using Microsoft.EntityFrameworkCore;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Application.UserBranches;
using SmartShopPOS.Contracts.Purchasing;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Persistence;
using SmartShopPOS.Infrastructure.Purchasing;
using SmartShopPOS.Infrastructure.UserBranches;

namespace SmartShopPOS.IntegrationTests;

[Collection("PostgreSQL integration")]
public sealed class PurchaseOrderPersistenceTests
{
    [PostgresFact]
    public async Task PurchaseOrders_AreTenantScopedAndEnforceDraftLifecycleAndDatabaseConstraints()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")!;
        var options = new DbContextOptionsBuilder<SmartShopPosDbContext>().UseNpgsql(connectionString).Options;
        var organization = new Organization("PO test organization", $"po-{Guid.NewGuid():N}");
        var otherOrganization = new Organization("Other PO organization", $"po-other-{Guid.NewGuid():N}");
        var user = new User(organization.Id, $"po-{Guid.NewGuid():N}@example.test", "PO user", "test-hash");
        var otherUser = new User(otherOrganization.Id, $"po-other-{Guid.NewGuid():N}@example.test", "Other PO user", "test-hash");
        var branch = new Branch(organization.Id, "MAIN", "Main branch");
        var unassignedBranch = new Branch(organization.Id, "SECOND", "Unassigned branch");
        var inactiveBranch = new Branch(organization.Id, "CLOSED", "Inactive branch");
        inactiveBranch.SetActive(false);
        var otherBranch = new Branch(otherOrganization.Id, "MAIN", "Other main branch");
        var supplier = new Supplier(organization.Id, "SUP-001", "Supplier");
        var inactiveSupplier = new Supplier(organization.Id, "SUP-002", "Inactive supplier");
        inactiveSupplier.SetActive(false);
        var otherSupplier = new Supplier(otherOrganization.Id, "SUP-001", "Other supplier");
        var sessionId = Guid.NewGuid();
        var session = new AuthenticationSession(user.Id, organization.Id, sessionId, $"hash-{Guid.NewGuid():N}");
        session.SetSelectedBranch(branch.Id);
        var assignment = new UserBranch(organization.Id, user.Id, branch.Id);

        await using (var setup = new SmartShopPosDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.AddRange(organization, otherOrganization, user, otherUser, branch, unassignedBranch, inactiveBranch,
                otherBranch, supplier, inactiveSupplier, otherSupplier, session, assignment);
            await setup.SaveChangesAsync();
        }

        try
        {
            await using var context = new SmartShopPosDbContext(options);
            var currentUser = new TestCurrentUser(organization.Id, user.Id, sessionId, branch.Id);
            var permissions = new TestPermissionChecker();
            IBranchAccessService branchAccess = new BranchAccessService(context, currentUser, permissions);
            var service = new PurchaseOrderService(context, currentUser, permissions, branchAccess);
            var request = new PurchaseOrderUpsertRequest(supplier.Id, branch.Id, DateTimeOffset.UtcNow, null, "  test order ");

            var created = await service.CreateAsync(request);
            Assert.True(created.IsSuccess, created.Message);
            Assert.Equal("PO-000001", created.Value!.OrderNumber);
            Assert.Equal("Draft", created.Value.Status);
            Assert.Equal(organization.Id, await context.PurchaseOrders.Where(order => order.Id == created.Value.Id)
                .Select(order => order.OrganizationId).SingleAsync());
            Assert.Equal(user.Id, created.Value.CreatedByUserId);
            Assert.Null(created.Value.UpdatedByUserId);

            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.NotFound,
                (await service.CreateAsync(request with { SupplierId = otherSupplier.Id })).Error);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.NotFound,
                (await service.CreateAsync(request with { BranchId = otherBranch.Id })).Error);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.InactiveSupplier,
                (await service.CreateAsync(request with { SupplierId = inactiveSupplier.Id })).Error);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.InactiveBranch,
                (await service.CreateAsync(request with { BranchId = inactiveBranch.Id })).Error);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.Forbidden,
                (await service.CreateAsync(request with { BranchId = unassignedBranch.Id })).Error);

            var deniedPermissions = new TestPermissionChecker("purchase_orders.view");
            var deniedBranchAccess = new BranchAccessService(context, currentUser, deniedPermissions);
            var deniedService = new PurchaseOrderService(context, currentUser, deniedPermissions, deniedBranchAccess);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.Forbidden,
                (await deniedService.CreateAsync(request)).Error);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.Forbidden,
                (await deniedService.UpdateAsync(created.Value.Id, request)).Error);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.Forbidden,
                (await deniedService.SubmitAsync(created.Value.Id)).Error);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.Forbidden,
                (await deniedService.CancelAsync(created.Value.Id)).Error);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.Forbidden,
                (await new PurchaseOrderService(context, currentUser, new TestPermissionChecker(false),
                    new BranchAccessService(context, currentUser, new TestPermissionChecker(false))).ListAsync()).Error);

            var sameOrgSecond = await service.CreateAsync(request);
            Assert.True(sameOrgSecond.IsSuccess, sameOrgSecond.Message);
            Assert.Equal("PO-000002", sameOrgSecond.Value!.OrderNumber);

            var concurrentCreations = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
            {
                await using var concurrentContext = new SmartShopPosDbContext(options);
                var concurrentAccess = new BranchAccessService(concurrentContext, currentUser, permissions);
                var concurrentService = new PurchaseOrderService(concurrentContext, currentUser, permissions, concurrentAccess);
                return await concurrentService.CreateAsync(request);
            }));
            Assert.All(concurrentCreations, result => Assert.True(result.IsSuccess, result.Message));
            Assert.Equal(4, concurrentCreations.Select(result => result.Value!.OrderNumber).Distinct().Count());

            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.NotFound,
                (await service.GetAsync(Guid.NewGuid())).Error);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.NotFound,
                (await service.UpdateAsync(Guid.NewGuid(), request)).Error);

            var updated = await service.UpdateAsync(created.Value.Id, request with { Notes = "Updated" });
            Assert.True(updated.IsSuccess, updated.Message);
            Assert.Equal("Updated", updated.Value!.Notes);

            var submitted = await service.SubmitAsync(created.Value.Id);
            Assert.True(submitted.IsSuccess, submitted.Message);
            Assert.Equal("Submitted", submitted.Value!.Status);
            Assert.Equal(user.Id, submitted.Value.UpdatedByUserId);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.Conflict,
                (await service.SubmitAsync(created.Value.Id)).Error);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.Conflict,
                (await service.UpdateAsync(created.Value.Id, request)).Error);

            var cancelled = await service.CancelAsync(created.Value.Id);
            Assert.True(cancelled.IsSuccess, cancelled.Message);
            Assert.Equal("Cancelled", cancelled.Value!.Status);
            Assert.Equal(user.Id, cancelled.Value.UpdatedByUserId);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.Conflict,
                (await service.CancelAsync(created.Value.Id)).Error);
            Assert.Equal(6, await context.PurchaseOrders.CountAsync(order => order.OrganizationId == organization.Id));
            Assert.Empty(await context.StockMovements.Where(movement => movement.OrganizationId == organization.Id).ToListAsync());

            var organizationBOrder = new PurchaseOrder(otherOrganization.Id, otherSupplier.Id, otherBranch.Id,
                "PO-OTHER-000001", DateTimeOffset.UtcNow, null, null, otherUser.Id);
            context.PurchaseOrders.Add(organizationBOrder);
            await context.SaveChangesAsync();
            Assert.All((await service.ListAsync()).Value!, order => Assert.DoesNotContain("OTHER", order.OrderNumber));

            var otherTenantUser = new TestCurrentUser(otherOrganization.Id, otherUser.Id, Guid.NewGuid(), otherBranch.Id);
            var otherPermissions = new TestPermissionChecker();
            var otherAccess = new BranchAccessService(context, otherTenantUser, otherPermissions);
            var otherService = new PurchaseOrderService(context, otherTenantUser, otherPermissions, otherAccess);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.NotFound,
                (await otherService.GetAsync(created.Value.Id)).Error);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.NotFound,
                (await otherService.UpdateAsync(created.Value.Id, request)).Error);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.NotFound,
                (await otherService.SubmitAsync(created.Value.Id)).Error);
            Assert.Equal(SmartShopPOS.Application.Purchasing.PurchaseOrderError.NotFound,
                (await otherService.CancelAsync(created.Value.Id)).Error);
            Assert.Single((await otherService.ListAsync()).Value!);

            context.PurchaseOrders.Add(new PurchaseOrder(organization.Id, otherSupplier.Id, branch.Id,
                "PO-CROSS-TENANT", DateTimeOffset.UtcNow, null, null, user.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();
            context.PurchaseOrders.Add(new PurchaseOrder(organization.Id, supplier.Id, otherBranch.Id,
                "PO-CROSS-BRANCH", DateTimeOffset.UtcNow, null, null, user.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();
            context.PurchaseOrders.Add(new PurchaseOrder(organization.Id, supplier.Id, branch.Id,
                "PO-000001", DateTimeOffset.UtcNow, null, null, user.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
        finally
        {
            await using var cleanup = new SmartShopPosDbContext(options);
            var organizationIds = new[] { organization.Id, otherOrganization.Id };
            cleanup.PurchaseOrders.RemoveRange(await cleanup.PurchaseOrders.Where(order => organizationIds.Contains(order.OrganizationId)).ToListAsync());
            cleanup.PurchaseOrderNumberSequences.RemoveRange(await cleanup.PurchaseOrderNumberSequences
                .Where(sequence => organizationIds.Contains(sequence.OrganizationId)).ToListAsync());
            cleanup.AuthenticationSessions.RemoveRange(await cleanup.AuthenticationSessions.Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync());
            cleanup.UserBranches.RemoveRange(await cleanup.UserBranches.Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync());
            cleanup.Suppliers.RemoveRange(await cleanup.Suppliers.Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync());
            cleanup.Branches.RemoveRange(await cleanup.Branches.Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync());
            cleanup.Users.RemoveRange(await cleanup.Users.Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync());
            cleanup.Organizations.RemoveRange(await cleanup.Organizations.Where(item => organizationIds.Contains(item.Id)).ToListAsync());
            await cleanup.SaveChangesAsync();
        }
    }

    private sealed class TestCurrentUser(Guid organizationId, Guid userId, Guid sessionId, Guid branchId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public Guid? OrganizationId => organizationId;
        public Guid? SessionId => sessionId;
        public Guid? SelectedBranchId => branchId;
        public string? Email => null;
    }

    private sealed class TestPermissionChecker : IPermissionChecker
    {
        private readonly string[] allowedPermissions;
        private readonly bool allowAll;

        public TestPermissionChecker(params string[] allowedPermissions)
            : this(allowedPermissions.Length == 0, allowedPermissions)
        {
        }

        public TestPermissionChecker(bool allowAll, params string[] allowedPermissions)
        {
            this.allowAll = allowAll;
            this.allowedPermissions = allowedPermissions;
        }

        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(allowAll || allowedPermissions.Contains(permissionKey, StringComparer.Ordinal));
    }
}
