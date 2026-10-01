using Microsoft.EntityFrameworkCore;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Application.Purchasing;
using SmartShopPOS.Contracts.Purchasing;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Persistence;
using SmartShopPOS.Infrastructure.Purchasing;
using SmartShopPOS.Infrastructure.UserBranches;

namespace SmartShopPOS.IntegrationTests;

[Collection("PostgreSQL integration")]
public sealed class GoodsReceiptPersistenceTests
{
    [PostgresFact]
    public async Task GoodsReceipt_PostsStockAtomicallyAndIsIdempotentAndCannotOverReceive()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")!;
        var options = new DbContextOptionsBuilder<SmartShopPosDbContext>().UseNpgsql(connectionString).Options;
        var org = new Organization("Receipt test", $"receipt-{Guid.NewGuid():N}");
        var user = new User(org.Id, $"receipt-{Guid.NewGuid():N}@example.test", "Receipt operator", "test-hash");
        var branch = new Branch(org.Id, "MAIN", "Main");
        var supplier = new Supplier(org.Id, "SUP-001", "Supplier");
        var category = new Category(org.Id, "General");
        var brand = new Brand(org.Id, "House");
        var unit = new UnitOfMeasure(org.Id, "KG", "Kilogram", "kg");
        var tax = new TaxCategory(org.Id, "VAT", "VAT", 16m);
        var product = new Product(org.Id, "RICE", null, "Rice", null, category.Id, brand.Id, unit.Id, tax.Id);
        var order = new PurchaseOrder(org.Id, supplier.Id, branch.Id, $"PO-{Guid.NewGuid():N}"[..20], DateTimeOffset.UtcNow, null, null, user.Id);
        order.Submit(user.Id);
        var poLine = new PurchaseOrderLine(org.Id, order.Id, product.Id, 5m, 3m);
        var otherOrder = new PurchaseOrder(org.Id, supplier.Id, branch.Id, $"PO-{Guid.NewGuid():N}"[..20], DateTimeOffset.UtcNow, null, null, user.Id);
        otherOrder.Submit(user.Id);
        var otherPoLine = new PurchaseOrderLine(org.Id, otherOrder.Id, product.Id, 5m, 3m);
        var sessionId = Guid.NewGuid();
        var session = new AuthenticationSession(user.Id, org.Id, sessionId, $"receipt-session-{Guid.NewGuid():N}");
        session.SetSelectedBranch(branch.Id);
        var assignment = new UserBranch(org.Id, user.Id, branch.Id);
        await using (var setup = new SmartShopPosDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.AddRange(org, user, branch, supplier, category, brand, unit, tax, product, order, poLine, otherOrder, otherPoLine, session, assignment);
            await setup.SaveChangesAsync();
        }

        try
        {
            var currentUser = new TestCurrentUser(org.Id, user.Id, sessionId, branch.Id);
            var permission = new AllowPermissions();
            GoodsReceiptResult<GoodsReceiptResponse> first;
            await using (var context = new SmartShopPosDbContext(options))
            {
                var access = new BranchAccessService(context, currentUser, permission);
                var service = new GoodsReceiptService(context, currentUser, permission, access);
                var request = new CreateGoodsReceiptRequest(DateTimeOffset.UtcNow, "Initial delivery", [new(poLine.Id, 3m)]);
                first = await service.CreateAsync(order.Id, request, "receipt-key-1");
                Assert.True(first.IsSuccess, first.Message);
                Assert.Equal("GR-000001", first.Value!.ReceiptNumber);
                Assert.Equal(3m, first.Value.Lines.Single().QuantityReceived);
                var replay = await service.CreateAsync(order.Id, request, "receipt-key-1");
                Assert.True(replay.IsSuccess, replay.Message);
                Assert.True(replay.WasReplay);
                Assert.Equal(first.Value.Id, replay.Value!.Id);
                Assert.Equal(GoodsReceiptError.Conflict,
                    (await service.CreateAsync(order.Id, request with { Notes = "changed" }, "receipt-key-1")).Error);
                var concurrent = await Task.WhenAll(Enumerable.Range(0, 2).Select(async index =>
                {
                    await using var concurrentContext = new SmartShopPosDbContext(options);
                    var concurrentAccess = new SmartShopPOS.Infrastructure.UserBranches.BranchAccessService(concurrentContext, currentUser, permission);
                    var concurrentService = new GoodsReceiptService(concurrentContext, currentUser, permission, concurrentAccess);
                    return await concurrentService.CreateAsync(order.Id,
                        new CreateGoodsReceiptRequest(DateTimeOffset.UtcNow, null, [new(poLine.Id, 1.5m)]), $"receipt-race-{index}");
                }));
                Assert.Single(concurrent, result => result.IsSuccess);
                Assert.Single(concurrent, result => result.Error == GoodsReceiptError.Conflict);
                Assert.Equal("GR-000002", concurrent.Single(result => result.IsSuccess).Value!.ReceiptNumber);
                Assert.Equal(GoodsReceiptError.Conflict,
                    (await service.CreateAsync(order.Id, new CreateGoodsReceiptRequest(DateTimeOffset.UtcNow, null, [new(poLine.Id, 0.6m)]), "receipt-key-3")).Error);
                Assert.Equal(GoodsReceiptError.Invalid,
                    (await service.CreateAsync(order.Id, new CreateGoodsReceiptRequest(DateTimeOffset.UtcNow, null, [new(poLine.Id, 1m), new(poLine.Id, 1m)]), "receipt-key-4")).Error);
            }

            await using (var integrityCheck = new SmartShopPosDbContext(options))
            {
                integrityCheck.GoodsReceiptLines.Add(new GoodsReceiptLine(org.Id, first.Value!.Id,
                    otherOrder.Id, otherPoLine.Id, 1m));
                await Assert.ThrowsAsync<DbUpdateException>(() => integrityCheck.SaveChangesAsync());
                integrityCheck.ChangeTracker.Clear();
            }

            await using var verify = new SmartShopPosDbContext(options);
            Assert.Equal(2, await verify.GoodsReceipts.CountAsync(x => x.OrganizationId == org.Id));
            Assert.Equal(2, await verify.GoodsReceiptLines.CountAsync(x => x.OrganizationId == org.Id));
            Assert.Equal(2, await verify.StockMovements.CountAsync(x => x.OrganizationId == org.Id && x.ReferenceType == "GoodsReceipt"));
            var movements = await verify.StockMovements.Where(x => x.OrganizationId == org.Id && x.ReferenceType == "GoodsReceipt").ToListAsync();
            Assert.All(movements, movement =>
            {
                Assert.Equal(MovementType.Receipt, movement.MovementType);
                Assert.Equal(branch.Id, movement.BranchId);
                Assert.Equal(product.Id, movement.ProductId);
                Assert.Equal(user.Id, movement.CreatedByUserId);
                Assert.True(movement.Quantity > 0m);
            });
            var balance = await verify.InventoryBalances.SingleAsync(x => x.OrganizationId == org.Id && x.BranchId == branch.Id && x.ProductId == product.Id);
            Assert.Equal(4.5m, balance.QuantityOnHand);
        }
        finally
        {
            await using var cleanup = new SmartShopPosDbContext(options);
            cleanup.GoodsReceiptIdempotency.RemoveRange(await cleanup.GoodsReceiptIdempotency.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.StockMovements.RemoveRange(await cleanup.StockMovements.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.GoodsReceiptLines.RemoveRange(await cleanup.GoodsReceiptLines.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.GoodsReceipts.RemoveRange(await cleanup.GoodsReceipts.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.PurchaseOrderLines.RemoveRange(await cleanup.PurchaseOrderLines.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.InventoryBalances.RemoveRange(await cleanup.InventoryBalances.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.PurchaseOrders.RemoveRange(await cleanup.PurchaseOrders.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.UserBranches.RemoveRange(await cleanup.UserBranches.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.AuthenticationSessions.RemoveRange(await cleanup.AuthenticationSessions.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.Products.RemoveRange(await cleanup.Products.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.Suppliers.RemoveRange(await cleanup.Suppliers.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.Branches.RemoveRange(await cleanup.Branches.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.Categories.RemoveRange(await cleanup.Categories.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.Brands.RemoveRange(await cleanup.Brands.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.UnitsOfMeasure.RemoveRange(await cleanup.UnitsOfMeasure.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.TaxCategories.RemoveRange(await cleanup.TaxCategories.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.GoodsReceiptNumberSequences.RemoveRange(await cleanup.GoodsReceiptNumberSequences.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.Users.RemoveRange(await cleanup.Users.Where(x => x.OrganizationId == org.Id).ToListAsync());
            cleanup.Organizations.RemoveRange(await cleanup.Organizations.Where(x => x.Id == org.Id).ToListAsync());
            await cleanup.SaveChangesAsync();
        }
    }

    private sealed class TestCurrentUser(Guid organizationId, Guid userId, Guid sessionId, Guid selectedBranchId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public Guid? OrganizationId => organizationId;
        public Guid? SessionId => sessionId;
        public Guid? SelectedBranchId => selectedBranchId;
        public string? Email => null;
    }

    private sealed class AllowPermissions : IPermissionChecker
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
