using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Application.Purchasing;
using SmartShopPOS.Application.UserBranches;
using SmartShopPOS.Contracts.Purchasing;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Persistence;
using SmartShopPOS.Infrastructure.Purchasing;
using SmartShopPOS.Infrastructure.UserBranches;

namespace SmartShopPOS.IntegrationTests;

[Collection("PostgreSQL integration")]
public sealed class PurchaseOrderLinePersistenceTests
{
    [PostgresFact]
    public async Task PurchaseOrderLines_EnforceTenantProductLifecyclePermissionsAndNoInventoryEffects()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")!;
        var options = new DbContextOptionsBuilder<SmartShopPosDbContext>().UseNpgsql(connectionString).Options;
        var organization = new Organization("PO line test organization", $"po-line-{Guid.NewGuid():N}");
        var otherOrganization = new Organization("Other PO line organization", $"po-line-other-{Guid.NewGuid():N}");
        var user = new User(organization.Id, $"po-line-{Guid.NewGuid():N}@example.test", "PO line user", "test-hash");
        var otherUser = new User(otherOrganization.Id, $"po-line-other-{Guid.NewGuid():N}@example.test", "Other PO line user", "test-hash");
        var branch = new Branch(organization.Id, "MAIN", "Main");
        var otherBranch = new Branch(otherOrganization.Id, "MAIN", "Other main");
        var otherAssignedBranch = new Branch(organization.Id, "SECOND", "Second branch");
        var unassignedBranch = new Branch(organization.Id, "THIRD", "Unassigned branch");
        var supplier = new Supplier(organization.Id, "SUP-001", "Supplier");
        var otherSupplier = new Supplier(otherOrganization.Id, "SUP-001", "Other supplier");
        var category = new Category(organization.Id, "General");
        var otherCategory = new Category(otherOrganization.Id, "General");
        var brand = new Brand(organization.Id, "House");
        var otherBrand = new Brand(otherOrganization.Id, "House");
        var unit = new UnitOfMeasure(organization.Id, "KG", "Kilogram", "kg");
        var otherUnit = new UnitOfMeasure(otherOrganization.Id, "KG", "Kilogram", "kg");
        var tax = new TaxCategory(organization.Id, "VAT", "VAT", 16m);
        var otherTax = new TaxCategory(otherOrganization.Id, "VAT", "VAT", 16m);
        var firstProduct = new Product(organization.Id, "RICE", null, "Rice", null, category.Id, brand.Id, unit.Id, tax.Id);
        var secondProduct = new Product(organization.Id, "FLOUR", null, "Flour", null, category.Id, brand.Id, unit.Id, tax.Id);
        var concurrentProduct = new Product(organization.Id, "OIL", null, "Oil", null, category.Id, brand.Id, unit.Id, tax.Id);
        var inactiveProduct = new Product(organization.Id, "OLD", null, "Old product", null, category.Id, brand.Id, unit.Id, tax.Id);
        inactiveProduct.SetActive(false);
        var foreignProduct = new Product(otherOrganization.Id, "RICE", null, "Foreign rice", null,
            otherCategory.Id, otherBrand.Id, otherUnit.Id, otherTax.Id);
        var sessionId = Guid.NewGuid();
        var session = new AuthenticationSession(user.Id, organization.Id, sessionId, $"hash-{Guid.NewGuid():N}");
        session.SetSelectedBranch(branch.Id);
        var otherSessionId = Guid.NewGuid();
        var otherSession = new AuthenticationSession(otherUser.Id, otherOrganization.Id, otherSessionId, $"hash-{Guid.NewGuid():N}");
        otherSession.SetSelectedBranch(otherBranch.Id);
        var assignment = new UserBranch(organization.Id, user.Id, branch.Id);
        var otherBranchAssignment = new UserBranch(organization.Id, user.Id, otherAssignedBranch.Id);
        var otherAssignment = new UserBranch(otherOrganization.Id, otherUser.Id, otherBranch.Id);
        var draftOrder = CreateOrder(organization, supplier, branch, user, "PO-LINE-001");
        var submittedOrder = CreateOrder(organization, supplier, branch, user, "PO-LINE-002");
        var cancelledOrder = CreateOrder(organization, supplier, branch, user, "PO-LINE-003");
        submittedOrder.Submit(user.Id);
        cancelledOrder.Cancel(user.Id);
        var foreignOrder = CreateOrder(otherOrganization, otherSupplier, otherBranch, otherUser, "PO-LINE-OTHER");
        var submittedLine = new PurchaseOrderLine(organization.Id, submittedOrder.Id, firstProduct.Id, 1m, 1m);
        var cancelledLine = new PurchaseOrderLine(organization.Id, cancelledOrder.Id, firstProduct.Id, 1m, 1m);
        var foreignLine = new PurchaseOrderLine(otherOrganization.Id, foreignOrder.Id, foreignProduct.Id, 1m, 1m);
        var initialBalance = new InventoryBalance(organization.Id, branch.Id, firstProduct.Id, 7.5m);

        await using (var setup = new SmartShopPosDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.AddRange(organization, otherOrganization, user, otherUser, branch, otherBranch, otherAssignedBranch, unassignedBranch,
                supplier, otherSupplier, category, otherCategory, brand, otherBrand, unit, otherUnit, tax, otherTax,
                firstProduct, secondProduct, concurrentProduct, inactiveProduct, foreignProduct, session, otherSession, assignment,
                otherBranchAssignment, otherAssignment, draftOrder, submittedOrder, cancelledOrder, foreignOrder,
                submittedLine, cancelledLine, foreignLine, initialBalance);
            await setup.SaveChangesAsync();
        }

        try
        {
            await using var context = new SmartShopPosDbContext(options);
            var currentUser = new TestCurrentUser(organization.Id, user.Id, sessionId, branch.Id);
            var permissions = new TestPermissionChecker();
            var access = new BranchAccessService(context, currentUser, permissions);
            var service = new PurchaseOrderLineService(context, currentUser, permissions, access);

            var created = await service.CreateAsync(draftOrder.Id, new PurchaseOrderLineUpsertRequest(firstProduct.Id, 2.5m, 0m, " initial "));
            Assert.True(created.IsSuccess, created.Message);
            Assert.Equal(2.5m, created.Value!.Quantity);
            Assert.Equal(0m, created.Value.UnitCost);
            Assert.Equal(0m, created.Value.LineTotal);
            Assert.Equal("initial", created.Value.Notes);
            Assert.Equal(draftOrder.Id, created.Value.PurchaseOrderId);
            Assert.Equal(organization.Id, await context.PurchaseOrderLines.Where(line => line.Id == created.Value.Id)
                .Select(line => line.OrganizationId).SingleAsync());

            var concurrentAdds = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            {
                await using var concurrentContext = new SmartShopPosDbContext(options);
                var concurrentAccess = new BranchAccessService(concurrentContext, currentUser, permissions);
                var concurrentService = new PurchaseOrderLineService(concurrentContext, currentUser, permissions, concurrentAccess);
                return await concurrentService.CreateAsync(draftOrder.Id,
                    new PurchaseOrderLineUpsertRequest(concurrentProduct.Id, 1m, 1m, null));
            }));
            Assert.Single(concurrentAdds, result => result.IsSuccess);
            Assert.Single(concurrentAdds, result => result.Error == PurchaseOrderLineError.Conflict);

            var duplicate = await service.CreateAsync(draftOrder.Id, new PurchaseOrderLineUpsertRequest(firstProduct.Id, 1m, 1m, null));
            Assert.Equal(PurchaseOrderLineError.Conflict, duplicate.Error);
            Assert.Equal(PurchaseOrderLineError.Invalid,
                (await service.CreateAsync(draftOrder.Id, new PurchaseOrderLineUpsertRequest(firstProduct.Id, 0m, 1m, null))).Error);
            Assert.Equal(PurchaseOrderLineError.Invalid,
                (await service.CreateAsync(draftOrder.Id, new PurchaseOrderLineUpsertRequest(firstProduct.Id, -1m, 1m, null))).Error);
            Assert.Equal(PurchaseOrderLineError.Invalid,
                (await service.CreateAsync(draftOrder.Id, new PurchaseOrderLineUpsertRequest(firstProduct.Id, 1m, -0.01m, null))).Error);
            Assert.Equal(PurchaseOrderLineError.InactiveProduct,
                (await service.CreateAsync(draftOrder.Id, new PurchaseOrderLineUpsertRequest(inactiveProduct.Id, 1m, 1m, null))).Error);
            Assert.Equal(PurchaseOrderLineError.NotFound,
                (await service.CreateAsync(draftOrder.Id, new PurchaseOrderLineUpsertRequest(foreignProduct.Id, 1m, 1m, null))).Error);
            Assert.Equal(PurchaseOrderLineError.NotFound,
                (await service.CreateAsync(draftOrder.Id, new PurchaseOrderLineUpsertRequest(Guid.NewGuid(), 1m, 1m, null))).Error);

            var second = await service.CreateAsync(draftOrder.Id, new PurchaseOrderLineUpsertRequest(secondProduct.Id, 1m, 3.25m, "second"));
            Assert.True(second.IsSuccess, second.Message);
            var changed = await service.UpdateAsync(draftOrder.Id, created.Value.Id,
                new PurchaseOrderLineUpsertRequest(secondProduct.Id, 4.25m, 2.125m, " changed "));
            Assert.Equal(PurchaseOrderLineError.Conflict, changed.Error);
            changed = await service.UpdateAsync(draftOrder.Id, created.Value.Id,
                new PurchaseOrderLineUpsertRequest(firstProduct.Id, 3.75m, 4.5m, " changed "));
            Assert.True(changed.IsSuccess, changed.Message);
            Assert.Equal(16.875m, changed.Value!.LineTotal);
            Assert.Equal("changed", changed.Value.Notes);

            var listed = await service.ListAsync(draftOrder.Id);
            Assert.True(listed.IsSuccess, listed.Message);
            Assert.Equal(3, listed.Value!.Count);
            Assert.Equal("Rice", listed.Value.Single(line => line.ProductId == firstProduct.Id).ProductName);
            Assert.All(listed.Value, line =>
            {
                Assert.Equal(0m, line.ReceivedQuantity);
                Assert.Equal(line.Quantity, line.RemainingQuantity);
                Assert.False(line.IsFullyReceived);
            });
            var cancelledProgress = await service.ListAsync(cancelledOrder.Id);
            Assert.True(cancelledProgress.IsSuccess, cancelledProgress.Message);
            var cancelledLines = cancelledProgress.Value!;
            Assert.Equal(0m, cancelledLines.Single().ReceivedQuantity);
            Assert.Equal(cancelledLine.Quantity, cancelledLines.Single().RemainingQuantity);
            Assert.False(cancelledLines.Single().IsFullyReceived);

            Assert.Equal(PurchaseOrderLineError.NotFound, (await service.ListAsync(foreignOrder.Id)).Error);
            Assert.Equal(PurchaseOrderLineError.NotFound,
                (await service.CreateAsync(foreignOrder.Id, new PurchaseOrderLineUpsertRequest(firstProduct.Id, 1m, 1m, null))).Error);
            Assert.Equal(PurchaseOrderLineError.NotFound, (await service.UpdateAsync(foreignOrder.Id, foreignLine.Id,
                new PurchaseOrderLineUpsertRequest(foreignProduct.Id, 2m, 2m, null))).Error);
            Assert.Equal(PurchaseOrderLineError.NotFound, (await service.DeleteAsync(foreignOrder.Id, foreignLine.Id)).Error);

            var deniedPermissions = new TestPermissionChecker("purchase_orders.lines.view");
            var deniedAccess = new BranchAccessService(context, currentUser, deniedPermissions);
            var deniedService = new PurchaseOrderLineService(context, currentUser, deniedPermissions, deniedAccess);
            Assert.Equal(PurchaseOrderLineError.Forbidden, (await deniedService.CreateAsync(draftOrder.Id,
                new PurchaseOrderLineUpsertRequest(firstProduct.Id, 1m, 1m, null))).Error);
            Assert.Equal(PurchaseOrderLineError.Forbidden, (await deniedService.UpdateAsync(draftOrder.Id, created.Value.Id,
                new PurchaseOrderLineUpsertRequest(firstProduct.Id, 1m, 1m, null))).Error);
            Assert.Equal(PurchaseOrderLineError.Forbidden, (await deniedService.DeleteAsync(draftOrder.Id, created.Value.Id)).Error);
            Assert.Equal(PurchaseOrderLineError.Forbidden, (await new PurchaseOrderLineService(context, currentUser,
                new TestPermissionChecker(false), new BranchAccessService(context, currentUser, new TestPermissionChecker(false)))
                .ListAsync(draftOrder.Id)).Error);

            var wrongBranchUser = new TestCurrentUser(organization.Id, user.Id, sessionId, otherAssignedBranch.Id);
            var wrongBranchAccess = new BranchAccessService(context, wrongBranchUser, permissions);
            var wrongBranchService = new PurchaseOrderLineService(context, wrongBranchUser, permissions, wrongBranchAccess);
            var selectedSession = await context.AuthenticationSessions.SingleAsync(item => item.SessionId == sessionId);
            selectedSession.SetSelectedBranch(otherAssignedBranch.Id);
            await context.SaveChangesAsync();
            Assert.Equal(PurchaseOrderLineError.Forbidden, (await wrongBranchService.ListAsync(draftOrder.Id)).Error);
            selectedSession.SetSelectedBranch(branch.Id);
            await context.SaveChangesAsync();
            selectedSession.SetSelectedBranch(unassignedBranch.Id);
            await context.SaveChangesAsync();
            Assert.Equal(PurchaseOrderLineError.Forbidden, (await service.ListAsync(draftOrder.Id)).Error);
            selectedSession.SetSelectedBranch(branch.Id);
            await context.SaveChangesAsync();

            Assert.Equal(PurchaseOrderLineError.Conflict, (await service.CreateAsync(submittedOrder.Id,
                new PurchaseOrderLineUpsertRequest(secondProduct.Id, 1m, 1m, null))).Error);
            Assert.Equal(PurchaseOrderLineError.Conflict, (await service.UpdateAsync(submittedOrder.Id, submittedLine.Id,
                new PurchaseOrderLineUpsertRequest(firstProduct.Id, 2m, 2m, null))).Error);
            Assert.Equal(PurchaseOrderLineError.Conflict, (await service.DeleteAsync(submittedOrder.Id, submittedLine.Id)).Error);

            Assert.Equal(PurchaseOrderLineError.Conflict, (await service.CreateAsync(cancelledOrder.Id,
                new PurchaseOrderLineUpsertRequest(secondProduct.Id, 1m, 1m, null))).Error);
            Assert.Equal(PurchaseOrderLineError.Conflict, (await service.UpdateAsync(cancelledOrder.Id, cancelledLine.Id,
                new PurchaseOrderLineUpsertRequest(firstProduct.Id, 2m, 2m, null))).Error);
            Assert.Equal(PurchaseOrderLineError.Conflict, (await service.DeleteAsync(cancelledOrder.Id, cancelledLine.Id)).Error);

            var deleted = await service.DeleteAsync(draftOrder.Id, second.Value!.Id);
            Assert.True(deleted.IsSuccess);
            Assert.DoesNotContain((await service.ListAsync(draftOrder.Id)).Value!, line => line.Id == second.Value.Id);
            var productChanged = await service.UpdateAsync(draftOrder.Id, created.Value.Id,
                new PurchaseOrderLineUpsertRequest(secondProduct.Id, 3.75m, 4.5m, "product changed"));
            Assert.True(productChanged.IsSuccess, productChanged.Message);
            Assert.Equal(secondProduct.Id, productChanged.Value!.ProductId);

            var otherCurrentUser = new TestCurrentUser(otherOrganization.Id, otherUser.Id, otherSessionId, otherBranch.Id);
            var otherAccess = new BranchAccessService(context, otherCurrentUser, permissions);
            var otherService = new PurchaseOrderLineService(context, otherCurrentUser, permissions, otherAccess);
            Assert.Equal(PurchaseOrderLineError.NotFound, (await otherService.ListAsync(draftOrder.Id)).Error);
            Assert.Equal(PurchaseOrderLineError.NotFound,
                (await otherService.UpdateAsync(draftOrder.Id, created.Value.Id,
                    new PurchaseOrderLineUpsertRequest(firstProduct.Id, 1m, 1m, null))).Error);
            Assert.Equal(PurchaseOrderLineError.NotFound, (await otherService.DeleteAsync(draftOrder.Id, created.Value.Id)).Error);
            Assert.Single((await otherService.ListAsync(foreignOrder.Id)).Value!);

            context.PurchaseOrderLines.Add(new PurchaseOrderLine(organization.Id, draftOrder.Id, foreignProduct.Id, 1m, 1m));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();
            context.PurchaseOrderLines.Add(new PurchaseOrderLine(otherOrganization.Id, draftOrder.Id, foreignProduct.Id, 1m, 1m));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();
            context.PurchaseOrderLines.Add(new PurchaseOrderLine(organization.Id, draftOrder.Id, secondProduct.Id, 1m, 1m));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO purchase_order_lines ("Id", "OrganizationId", "PurchaseOrderId", "ProductId", "Quantity", "UnitCost", "CreatedAt", "UpdatedAt")
                VALUES ({Guid.NewGuid()}, {organization.Id}, {draftOrder.Id}, {concurrentProduct.Id}, 0, 1, NOW(), NOW());
                """));
            await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO purchase_order_lines ("Id", "OrganizationId", "PurchaseOrderId", "ProductId", "Quantity", "UnitCost", "CreatedAt", "UpdatedAt")
                VALUES ({Guid.NewGuid()}, {organization.Id}, {draftOrder.Id}, {concurrentProduct.Id}, 1, -1, NOW(), NOW());
                """));

            context.ChangeTracker.Clear();
            Assert.Equal(7.5m, await context.InventoryBalances.Where(balance => balance.Id == initialBalance.Id)
                .Select(balance => balance.QuantityOnHand).SingleAsync());
            Assert.Empty(await context.StockMovements.Where(movement => movement.OrganizationId == organization.Id).ToListAsync());
        }
        finally
        {
            await using var cleanup = new SmartShopPosDbContext(options);
            var organizationIds = new[] { organization.Id, otherOrganization.Id };
            cleanup.PurchaseOrderLines.RemoveRange(await cleanup.PurchaseOrderLines.Where(line => organizationIds.Contains(line.OrganizationId)).ToListAsync());
            cleanup.InventoryBalances.RemoveRange(await cleanup.InventoryBalances.Where(balance => organizationIds.Contains(balance.OrganizationId)).ToListAsync());
            cleanup.PurchaseOrders.RemoveRange(await cleanup.PurchaseOrders.Where(order => organizationIds.Contains(order.OrganizationId)).ToListAsync());
            cleanup.UserBranches.RemoveRange(await cleanup.UserBranches.Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync());
            cleanup.AuthenticationSessions.RemoveRange(await cleanup.AuthenticationSessions.Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync());
            cleanup.Products.RemoveRange(await cleanup.Products.Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync());
            cleanup.Suppliers.RemoveRange(await cleanup.Suppliers.Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync());
            cleanup.Branches.RemoveRange(await cleanup.Branches.Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync());
            cleanup.Categories.RemoveRange(await cleanup.Categories.Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync());
            cleanup.Brands.RemoveRange(await cleanup.Brands.Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync());
            cleanup.UnitsOfMeasure.RemoveRange(await cleanup.UnitsOfMeasure.Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync());
            cleanup.TaxCategories.RemoveRange(await cleanup.TaxCategories.Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync());
            cleanup.Users.RemoveRange(await cleanup.Users.Where(item => organizationIds.Contains(item.OrganizationId)).ToListAsync());
            cleanup.Organizations.RemoveRange(await cleanup.Organizations.Where(item => organizationIds.Contains(item.Id)).ToListAsync());
            await cleanup.SaveChangesAsync();
        }
    }

    private static PurchaseOrder CreateOrder(Organization organization, Supplier supplier, Branch branch, User user, string number) =>
        new(organization.Id, supplier.Id, branch.Id, number, DateTimeOffset.UtcNow, null, null, user.Id);

    private sealed class TestCurrentUser(Guid organizationId, Guid userId, Guid sessionId, Guid selectedBranchId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public Guid? OrganizationId => organizationId;
        public Guid? SessionId => sessionId;
        public Guid? SelectedBranchId => selectedBranchId;
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
