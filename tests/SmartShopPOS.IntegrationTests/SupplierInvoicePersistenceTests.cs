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
public sealed class SupplierInvoicePersistenceTests
{
    [PostgresFact]
    public async Task SupplierInvoices_EnforceTenantAndDocumentRulesAndNeverReceiveInventory()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")!;
        var options = new DbContextOptionsBuilder<SmartShopPosDbContext>().UseNpgsql(connectionString).Options;
        var org = new Organization("Invoice test", $"invoice-{Guid.NewGuid():N}");
        var otherOrg = new Organization("Other invoice test", $"invoice-other-{Guid.NewGuid():N}");
        var user = new User(org.Id, $"invoice-{Guid.NewGuid():N}@example.test", "Invoice operator", "hash");
        var otherUser = new User(otherOrg.Id, $"invoice-other-{Guid.NewGuid():N}@example.test", "Other operator", "hash");
        var branch = new Branch(org.Id, "MAIN", "Main");
        var otherBranch = new Branch(otherOrg.Id, "MAIN", "Main");
        var supplier = new Supplier(org.Id, "SUP-001", "Supplier A");
        var otherSupplier = new Supplier(org.Id, "SUP-002", "Supplier B");
        var foreignSupplier = new Supplier(otherOrg.Id, "SUP-001", "Foreign supplier");
        var category = new Category(org.Id, "General");
        var brand = new Brand(org.Id, "House");
        var unit = new UnitOfMeasure(org.Id, "EA", "Each", "ea");
        var tax = new TaxCategory(org.Id, "VAT", "VAT", 16m);
        var product = new Product(org.Id, "SKU-1", null, "Sample", null, category.Id, brand.Id, unit.Id, tax.Id);
        var order = new PurchaseOrder(org.Id, supplier.Id, branch.Id, $"PO-{Guid.NewGuid():N}"[..20], DateTimeOffset.UtcNow, null, null, user.Id);
        order.Submit(user.Id);
        var orderLine = new PurchaseOrderLine(org.Id, order.Id, product.Id, 2.5m, 5m);
        var otherOrder = new PurchaseOrder(org.Id, otherSupplier.Id, branch.Id, $"PO-{Guid.NewGuid():N}"[..20], DateTimeOffset.UtcNow, null, null, user.Id);
        otherOrder.Submit(user.Id);
        var otherOrderLine = new PurchaseOrderLine(org.Id, otherOrder.Id, product.Id, 2.5m, 5m);
        var sessionId = Guid.NewGuid();
        var session = new AuthenticationSession(user.Id, org.Id, sessionId, $"invoice-session-{Guid.NewGuid():N}");
        session.SetSelectedBranch(branch.Id);
        var assignment = new UserBranch(org.Id, user.Id, branch.Id);
        await using (var setup = new SmartShopPosDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.AddRange(org, otherOrg, user, otherUser, branch, otherBranch, supplier, otherSupplier, foreignSupplier,
                category, brand, unit, tax, product, order, orderLine, otherOrder, otherOrderLine, session, assignment);
            await setup.SaveChangesAsync();
        }

        try
        {
            var current = new TestCurrentUser(org.Id, user.Id, sessionId, branch.Id);
            var permissions = new AllowPermissions();
            await using var db = new SmartShopPosDbContext(options);
            var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
            Assert.Contains("20261001080107_CreateSupplierInvoiceFoundation", appliedMigrations);
            Assert.Contains("20261001081105_FixSupplierInvoiceIntegrityTriggers", appliedMigrations);
            var branchAccess = new BranchAccessService(db, current, permissions);
            var invoiceService = new SupplierInvoiceService(db, current, permissions, branchAccess);
            var receiptService = new GoodsReceiptService(db, current, permissions, branchAccess);
            var receipt = await receiptService.CreateAsync(order.Id,
                new CreateGoodsReceiptRequest(DateTimeOffset.UtcNow, null, [new(orderLine.Id, 2.5m)]), $"invoice-receipt-{Guid.NewGuid():N}");
            Assert.True(receipt.IsSuccess, receipt.Message);
            var movementCount = await db.StockMovements.CountAsync(x => x.OrganizationId == org.Id);

            var create = new SupplierInvoiceCreateRequest(supplier.Id, order.Id, " INV/2026/001 ", new DateOnly(2026, 10, 1), null,
                12.5m, 1m, 0m, 13.5m);
            var created = await invoiceService.CreateAsync(create);
            Assert.True(created.IsSuccess, created.Message);
            Assert.Equal("SI-000001", created.Value!.InternalNumber);
            Assert.Equal(" INV/2026/001 ", created.Value.InvoiceNumber);
            var line = await invoiceService.AddLineAsync(created.Value.Id,
                new SupplierInvoiceLineRequest(orderLine.Id, "Sample", 2.5m, 5m, 1m, tax.Id));
            Assert.True(line.IsSuccess, line.Message);
            Assert.Equal(12.5m, line.Value!.NetAmount);
            var detail = await invoiceService.GetAsync(created.Value.Id);
            Assert.Equal(12.5m, detail.Value!.NetAmount);
            Assert.Equal(1m, detail.Value.TaxAmount);
            Assert.Equal(13.5m, detail.Value.GrossAmount);
            Assert.Equal(12.5m, detail.Value.SupplierDocumentNetAmount);
            Assert.Equal(13.5m, detail.Value.SupplierDocumentGrossAmount);
            var posted = await invoiceService.PostAsync(created.Value.Id);
            Assert.True(posted.IsSuccess, posted.Message);
            Assert.Equal("Posted", posted.Value!.Status);
            Assert.Equal(SupplierInvoiceError.Conflict, (await invoiceService.PostAsync(created.Value.Id)).Error);
            Assert.Equal(SupplierInvoiceError.Conflict, (await invoiceService.UpdateLineAsync(created.Value.Id, line.Value.Id,
                new SupplierInvoiceLineRequest(orderLine.Id, "Changed", 2.5m, 5m, 1m, tax.Id))).Error);
            Assert.Equal(movementCount, await db.StockMovements.CountAsync(x => x.OrganizationId == org.Id));
            Assert.Equal(SupplierInvoiceError.NotFound, (await invoiceService.GetAsync(Guid.NewGuid())).Error);

            var mismatchSupplier = await invoiceService.CreateAsync(create with { SupplierId = otherSupplier.Id, InvoiceNumber = "different" });
            Assert.Equal(SupplierInvoiceError.Invalid, mismatchSupplier.Error);
            var unauthorizedPermissions = new AllowPermissions("supplier_invoices.view");
            var deniedService = new SupplierInvoiceService(db, current, unauthorizedPermissions,
                new BranchAccessService(db, current, unauthorizedPermissions));
            Assert.Equal(SupplierInvoiceError.Forbidden, (await deniedService.CreateAsync(create with { InvoiceNumber = "denied" })).Error);
            Assert.Equal(SupplierInvoiceError.Forbidden, (await deniedService.PostAsync(created.Value.Id)).Error);

            var serials = await Task.WhenAll(Enumerable.Range(0, 4).Select(async i =>
            {
                await using var concurrentDb = new SmartShopPosDbContext(options);
                var concurrentService = new SupplierInvoiceService(concurrentDb, current, permissions,
                    new BranchAccessService(concurrentDb, current, permissions));
                return await concurrentService.CreateAsync(new SupplierInvoiceCreateRequest(supplier.Id, null,
                    $"SERIAL-{i}-{Guid.NewGuid():N}", new DateOnly(2026, 10, 1), null));
            }));
            Assert.All(serials, x => Assert.True(x.IsSuccess, x.Message));
            Assert.Equal(4, serials.Select(x => x.Value!.InternalNumber).Distinct().Count());

            var duplicate = await invoiceService.CreateAsync(create with { InvoiceNumber = "inv/2026/001" });
            Assert.Equal(SupplierInvoiceError.Conflict, duplicate.Error);

            var sameNumberDifferentSupplier = await invoiceService.CreateAsync(create with
            {
                SupplierId = otherSupplier.Id, PurchaseOrderId = null, InvoiceNumber = "INV/2026/001"
            });
            Assert.True(sameNumberDifferentSupplier.IsSuccess, sameNumberDifferentSupplier.Message);
            var nonPoLine = await invoiceService.AddLineAsync(sameNumberDifferentSupplier.Value!.Id,
                new SupplierInvoiceLineRequest(null, "Utilities", 1m, 20m, 0m));
            Assert.True(nonPoLine.IsSuccess, nonPoLine.Message);
            Assert.Equal(SupplierInvoiceError.Conflict, (await invoiceService.PostAsync(sameNumberDifferentSupplier.Value.Id)).Error);
            Assert.True((await invoiceService.CancelAsync(sameNumberDifferentSupplier.Value.Id)).IsSuccess);
            Assert.Equal(SupplierInvoiceError.Conflict, (await invoiceService.UpdateLineAsync(sameNumberDifferentSupplier.Value.Id,
                nonPoLine.Value!.Id, new SupplierInvoiceLineRequest(null, "Changed", 1m, 20m))).Error);

            var invalidTenantInvoice = new SupplierInvoice(org.Id, foreignSupplier.Id, null, "CROSS-TENANT", "CROSS-TENANT",
                "SI-CROSS", new DateOnly(2026, 10, 1), null, 0m, 0m, 0m, 0m, null, user.Id);
            await using (var integrityContext = new SmartShopPosDbContext(options))
            {
                integrityContext.SupplierInvoices.Add(invalidTenantInvoice);
                await Assert.ThrowsAsync<DbUpdateException>(() => integrityContext.SaveChangesAsync());
            }

            var integrityInvoice = await invoiceService.CreateAsync(create with { InvoiceNumber = "PO-LINE-INTEGRITY" });
            Assert.True(integrityInvoice.IsSuccess, integrityInvoice.Message);
            await using (var integrityContext = new SmartShopPosDbContext(options))
            {
                integrityContext.SupplierInvoiceLines.Add(new SupplierInvoiceLine(org.Id, integrityInvoice.Value!.Id,
                    order.Id, otherOrderLine.Id, "Wrong PO line", 1m, 5m, 0m));
                await Assert.ThrowsAsync<DbUpdateException>(() => integrityContext.SaveChangesAsync());
            }

            var foreignInvoice = new SupplierInvoice(otherOrg.Id, foreignSupplier.Id, null, "FOREIGN-1", "FOREIGN-1", "SI-000001",
                new DateOnly(2026, 10, 1), null, 0m, 0m, 0m, 0m, null, otherUser.Id);
            await using (var foreignContext = new SmartShopPosDbContext(options))
            {
                foreignContext.SupplierInvoices.Add(foreignInvoice);
                await foreignContext.SaveChangesAsync();
            }
            Assert.Equal(SupplierInvoiceError.NotFound, (await invoiceService.GetAsync(foreignInvoice.Id)).Error);

            Assert.Equal(SupplierInvoiceError.Conflict, (await invoiceService.CancelAsync(created.Value.Id)).Error);
            Assert.Empty(await db.StockMovements.Where(x => x.ReferenceId == created.Value.Id).ToListAsync());
        }
        finally
        {
            await using var cleanup = new SmartShopPosDbContext(options);
            var orgIds = new[] { org.Id, otherOrg.Id };
            cleanup.SupplierInvoiceLines.RemoveRange(await cleanup.SupplierInvoiceLines.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.SupplierInvoices.RemoveRange(await cleanup.SupplierInvoices.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.SupplierInvoiceNumberSequences.RemoveRange(await cleanup.SupplierInvoiceNumberSequences.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.StockMovements.RemoveRange(await cleanup.StockMovements.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.InventoryBalances.RemoveRange(await cleanup.InventoryBalances.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.GoodsReceiptLines.RemoveRange(await cleanup.GoodsReceiptLines.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.GoodsReceipts.RemoveRange(await cleanup.GoodsReceipts.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.GoodsReceiptIdempotency.RemoveRange(await cleanup.GoodsReceiptIdempotency.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.GoodsReceiptNumberSequences.RemoveRange(await cleanup.GoodsReceiptNumberSequences.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.PurchaseOrderLines.RemoveRange(await cleanup.PurchaseOrderLines.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.PurchaseOrders.RemoveRange(await cleanup.PurchaseOrders.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.PurchaseOrderNumberSequences.RemoveRange(await cleanup.PurchaseOrderNumberSequences.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.Products.RemoveRange(await cleanup.Products.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.Categories.RemoveRange(await cleanup.Categories.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.Brands.RemoveRange(await cleanup.Brands.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.UnitsOfMeasure.RemoveRange(await cleanup.UnitsOfMeasure.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.TaxCategories.RemoveRange(await cleanup.TaxCategories.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.UserBranches.RemoveRange(await cleanup.UserBranches.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.AuthenticationSessions.RemoveRange(await cleanup.AuthenticationSessions.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.Suppliers.RemoveRange(await cleanup.Suppliers.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.Branches.RemoveRange(await cleanup.Branches.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.Users.RemoveRange(await cleanup.Users.Where(x => orgIds.Contains(x.OrganizationId)).ToListAsync());
            cleanup.Organizations.RemoveRange(await cleanup.Organizations.Where(x => orgIds.Contains(x.Id)).ToListAsync());
            await cleanup.SaveChangesAsync();
        }
    }

    private sealed class TestCurrentUser(Guid organizationId, Guid userId, Guid sessionId, Guid branchId) : ICurrentUser
    {
        public bool IsAuthenticated => true; public Guid? UserId => userId; public Guid? OrganizationId => organizationId;
        public Guid? SessionId => sessionId; public Guid? SelectedBranchId => branchId; public string? Email => null;
    }
    private sealed class AllowPermissions(params string[] only) : IPermissionChecker
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(only.Length == 0 || only.Contains(permissionKey, StringComparer.Ordinal));
    }
}
