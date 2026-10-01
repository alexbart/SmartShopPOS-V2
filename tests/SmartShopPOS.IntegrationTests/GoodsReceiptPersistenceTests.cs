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
        var unassignedBranch = new Branch(org.Id, "ALT", "Unassigned");
        var supplier = new Supplier(org.Id, "SUP-001", "Supplier");
        var category = new Category(org.Id, "General");
        var brand = new Brand(org.Id, "House");
        var secondBrand = new Brand(org.Id, "Pantry");
        var unit = new UnitOfMeasure(org.Id, "KG", "Kilogram", "kg");
        var tax = new TaxCategory(org.Id, "VAT", "VAT", 16m);
        var product = new Product(org.Id, "RICE", null, "Rice", null, category.Id, brand.Id, unit.Id, tax.Id);
        var secondProduct = new Product(org.Id, "BEANS", null, "Beans", null, category.Id, secondBrand.Id, unit.Id, tax.Id);
        var order = new PurchaseOrder(org.Id, supplier.Id, branch.Id, $"PO-{Guid.NewGuid():N}"[..20], DateTimeOffset.UtcNow, null, null, user.Id);
        order.Submit(user.Id);
        var poLine = new PurchaseOrderLine(org.Id, order.Id, product.Id, 5m, 3m);
        var secondPoLine = new PurchaseOrderLine(org.Id, order.Id, secondProduct.Id, 3.75m, 2m);
        var otherOrder = new PurchaseOrder(org.Id, supplier.Id, branch.Id, $"PO-{Guid.NewGuid():N}"[..20], DateTimeOffset.UtcNow, null, null, user.Id);
        otherOrder.Submit(user.Id);
        var otherPoLine = new PurchaseOrderLine(org.Id, otherOrder.Id, product.Id, 5m, 3m);
        var emptyOrder = new PurchaseOrder(org.Id, supplier.Id, branch.Id, $"PO-{Guid.NewGuid():N}"[..20], DateTimeOffset.UtcNow, null, null, user.Id);
        var sessionId = Guid.NewGuid();
        var session = new AuthenticationSession(user.Id, org.Id, sessionId, $"receipt-session-{Guid.NewGuid():N}");
        session.SetSelectedBranch(branch.Id);
        var assignment = new UserBranch(org.Id, user.Id, branch.Id);
        await using (var setup = new SmartShopPosDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.AddRange(org, user, branch, unassignedBranch, supplier, category, brand, secondBrand, unit, tax, product, secondProduct,
                order, poLine, secondPoLine, otherOrder, otherPoLine, emptyOrder, session, assignment);
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
                var summaryService = new PurchaseOrderLineService(context, currentUser, permission, access);
                var initialSummary = await summaryService.GetReceivingSummaryAsync(order.Id);
                Assert.True(initialSummary.IsSuccess, initialSummary.Message);
                Assert.Equal("NotReceived", initialSummary.Value!.ReceivingState);
                Assert.Equal(8.75m, initialSummary.Value.OrderedQuantity);
                Assert.Equal(0m, initialSummary.Value.ReceivedQuantity);
                Assert.Equal(8.75m, initialSummary.Value.RemainingQuantity);
                Assert.Equal(2, initialSummary.Value.Lines.Count);
                var emptySummary = await summaryService.GetReceivingSummaryAsync(emptyOrder.Id);
                Assert.True(emptySummary.IsSuccess, emptySummary.Message);
                Assert.Equal("NotReceived", emptySummary.Value!.ReceivingState);
                Assert.Equal(0m, emptySummary.Value.OrderedQuantity);
                Assert.Empty(emptySummary.Value.Lines);

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

                var partial = await service.CreateAsync(order.Id,
                    new CreateGoodsReceiptRequest(DateTimeOffset.UtcNow, null, [new(secondPoLine.Id, 1.25m)]), "receipt-key-5");
                Assert.True(partial.IsSuccess, partial.Message);
                var completion = await service.CreateAsync(order.Id,
                    new CreateGoodsReceiptRequest(DateTimeOffset.UtcNow, null, [new(secondPoLine.Id, 2.5m)]), "receipt-key-6");
                Assert.True(completion.IsSuccess, completion.Message);

                await using (var otherOrderContext = new SmartShopPosDbContext(options))
                {
                    var otherOrderAccess = new BranchAccessService(otherOrderContext, currentUser, permission);
                    var otherOrderService = new GoodsReceiptService(otherOrderContext, currentUser, permission, otherOrderAccess);
                    var otherOrderReceipt = await otherOrderService.CreateAsync(otherOrder.Id,
                        new CreateGoodsReceiptRequest(DateTimeOffset.UtcNow, null, [new(otherPoLine.Id, 5m)]), "receipt-key-other-po");
                    Assert.True(otherOrderReceipt.IsSuccess, otherOrderReceipt.Message);
                }

                await using (var cancellationContext = new SmartShopPosDbContext(options))
                {
                    var cancellationAccess = new BranchAccessService(cancellationContext, currentUser, permission);
                    var purchaseOrders = new PurchaseOrderService(cancellationContext, currentUser, permission, cancellationAccess);
                    var cancelled = await purchaseOrders.CancelAsync(otherOrder.Id);
                    Assert.True(cancelled.IsSuccess, cancelled.Message);
                    var receiptService = new GoodsReceiptService(cancellationContext, currentUser, permission, cancellationAccess);
                    Assert.Equal(GoodsReceiptError.Conflict, (await receiptService.CreateAsync(otherOrder.Id,
                        new CreateGoodsReceiptRequest(DateTimeOffset.UtcNow, null, [new(otherPoLine.Id, 0.1m)]),
                        "receipt-cancelled-po")).Error);
                }

                var progress = await new PurchaseOrderLineService(context, currentUser, permission, access).ListAsync(order.Id);
                Assert.True(progress.IsSuccess, progress.Message);
                var progressLines = progress.Value!;
                var rice = progressLines.Single(line => line.Id == poLine.Id);
                Assert.Equal(5m, rice.Quantity);
                Assert.Equal(4.5m, rice.ReceivedQuantity);
                Assert.Equal(0.5m, rice.RemainingQuantity);
                Assert.False(rice.IsFullyReceived);
                var beans = progressLines.Single(line => line.Id == secondPoLine.Id);
                Assert.Equal(3.75m, beans.Quantity);
                Assert.Equal(3.75m, beans.ReceivedQuantity);
                Assert.Equal(0m, beans.RemainingQuantity);
                Assert.True(beans.IsFullyReceived);

                var summary = await summaryService.GetReceivingSummaryAsync(order.Id);
                Assert.True(summary.IsSuccess, summary.Message);
                Assert.Equal("Submitted", summary.Value!.Status);
                Assert.Equal("PartiallyReceived", summary.Value.ReceivingState);
                Assert.Equal(8.75m, summary.Value.OrderedQuantity);
                Assert.Equal(8.25m, summary.Value.ReceivedQuantity);
                Assert.Equal(0.5m, summary.Value.RemainingQuantity);
                Assert.Equal(2, summary.Value.Lines.Count);

                var otherProgress = await new PurchaseOrderLineService(context, currentUser, permission, access).ListAsync(otherOrder.Id);
                Assert.True(otherProgress.IsSuccess, otherProgress.Message);
                var otherRice = otherProgress.Value!.Single();
                Assert.Equal(5m, otherRice.Quantity);
                Assert.Equal(5m, otherRice.ReceivedQuantity);
                Assert.Equal(0m, otherRice.RemainingQuantity);
                Assert.True(otherRice.IsFullyReceived);
            }

            await using (var readOnlyContext = new SmartShopPosDbContext(options))
            {
                var countsBefore = (
                    await readOnlyContext.GoodsReceipts.CountAsync(x => x.OrganizationId == org.Id),
                    await readOnlyContext.StockMovements.CountAsync(x => x.OrganizationId == org.Id),
                    await readOnlyContext.InventoryBalances.CountAsync(x => x.OrganizationId == org.Id));
                var balancesBefore = await readOnlyContext.InventoryBalances.Where(x => x.OrganizationId == org.Id)
                    .OrderBy(x => x.Id).Select(x => new { x.Id, x.QuantityOnHand }).ToListAsync();
                var statusesBefore = await readOnlyContext.PurchaseOrders.Where(x => x.OrganizationId == org.Id)
                    .OrderBy(x => x.Id).Select(x => new { x.Id, x.Status }).ToListAsync();
                var readOnlyAccess = new BranchAccessService(readOnlyContext, currentUser, permission);
                var readOnlySummaryService = new PurchaseOrderLineService(readOnlyContext, currentUser, permission, readOnlyAccess);
                var cancelledSummary = await readOnlySummaryService.GetReceivingSummaryAsync(otherOrder.Id);
                Assert.True(cancelledSummary.IsSuccess, cancelledSummary.Message);
                Assert.Equal("Cancelled", cancelledSummary.Value!.Status);
                Assert.Equal("FullyReceived", cancelledSummary.Value.ReceivingState);
                Assert.Equal(5m, cancelledSummary.Value.ReceivedQuantity);
                Assert.True((await readOnlySummaryService.GetReceivingSummaryAsync(otherOrder.Id)).IsSuccess);
                var countsAfter = (
                    await readOnlyContext.GoodsReceipts.CountAsync(x => x.OrganizationId == org.Id),
                    await readOnlyContext.StockMovements.CountAsync(x => x.OrganizationId == org.Id),
                    await readOnlyContext.InventoryBalances.CountAsync(x => x.OrganizationId == org.Id));
                var balancesAfter = await readOnlyContext.InventoryBalances.Where(x => x.OrganizationId == org.Id)
                    .OrderBy(x => x.Id).Select(x => new { x.Id, x.QuantityOnHand }).ToListAsync();
                var statusesAfter = await readOnlyContext.PurchaseOrders.Where(x => x.OrganizationId == org.Id)
                    .OrderBy(x => x.Id).Select(x => new { x.Id, x.Status }).ToListAsync();
                Assert.Equal(countsBefore, countsAfter);
                Assert.Equal(balancesBefore, balancesAfter);
                Assert.Equal(statusesBefore, statusesAfter);
            }

            await using (var deniedContext = new SmartShopPosDbContext(options))
            {
                var deniedUser = new TestCurrentUser(org.Id, user.Id, sessionId, branch.Id);
                var deniedPermissions = new DenyPermissions();
                var deniedService = new PurchaseOrderLineService(deniedContext, deniedUser, deniedPermissions,
                    new BranchAccessService(deniedContext, deniedUser, deniedPermissions));
                Assert.Equal(PurchaseOrderLineError.Forbidden,
                    (await deniedService.GetReceivingSummaryAsync(order.Id)).Error);
            }

            await using (var wrongBranchContext = new SmartShopPosDbContext(options))
            {
                var wrongBranchUser = new TestCurrentUser(org.Id, user.Id, sessionId, Guid.NewGuid());
                var wrongBranchPermissions = new AllowPermissions();
                var wrongBranchService = new PurchaseOrderLineService(wrongBranchContext, wrongBranchUser, wrongBranchPermissions,
                    new BranchAccessService(wrongBranchContext, wrongBranchUser, wrongBranchPermissions));
                var persistedSession = await wrongBranchContext.AuthenticationSessions.SingleAsync(x => x.SessionId == sessionId);
                persistedSession.SetSelectedBranch(unassignedBranch.Id);
                await wrongBranchContext.SaveChangesAsync();
                Assert.Equal(PurchaseOrderLineError.Forbidden,
                    (await wrongBranchService.GetReceivingSummaryAsync(order.Id)).Error);
                persistedSession.SetSelectedBranch(branch.Id);
                await wrongBranchContext.SaveChangesAsync();
            }

            await using (var integrityCheck = new SmartShopPosDbContext(options))
            {
                integrityCheck.GoodsReceiptLines.Add(new GoodsReceiptLine(org.Id, first.Value!.Id,
                    otherOrder.Id, otherPoLine.Id, 1m));
                await Assert.ThrowsAsync<DbUpdateException>(() => integrityCheck.SaveChangesAsync());
                integrityCheck.ChangeTracker.Clear();
            }

            await using var verify = new SmartShopPosDbContext(options);
            Assert.Equal(5, await verify.GoodsReceipts.CountAsync(x => x.OrganizationId == org.Id));
            Assert.Equal(5, await verify.GoodsReceiptLines.CountAsync(x => x.OrganizationId == org.Id));
            Assert.Equal(5, await verify.StockMovements.CountAsync(x => x.OrganizationId == org.Id && x.ReferenceType == "GoodsReceipt"));
            var movements = await verify.StockMovements.Where(x => x.OrganizationId == org.Id && x.ReferenceType == "GoodsReceipt").ToListAsync();
            Assert.All(movements, movement =>
            {
                Assert.Equal(MovementType.Receipt, movement.MovementType);
                Assert.Equal(branch.Id, movement.BranchId);
                Assert.Contains(movement.ProductId, new[] { product.Id, secondProduct.Id });
                Assert.Equal(user.Id, movement.CreatedByUserId);
                Assert.True(movement.Quantity > 0m);
            });
            var balance = await verify.InventoryBalances.SingleAsync(x => x.OrganizationId == org.Id && x.BranchId == branch.Id && x.ProductId == product.Id);
            Assert.Equal(9.5m, balance.QuantityOnHand);
            Assert.Equal(3.75m, await verify.InventoryBalances.Where(x => x.OrganizationId == org.Id && x.BranchId == branch.Id && x.ProductId == secondProduct.Id)
                .Select(x => x.QuantityOnHand).SingleAsync());
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

    private sealed class DenyPermissions : IPermissionChecker
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
