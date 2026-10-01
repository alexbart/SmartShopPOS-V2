using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.IntegrationTests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Application_ShouldServeLiveHealthEndpoint()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_RequiresBothAuthenticationCookiesOnlyForProtectedEndpoints()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var branchSecurity = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/branches")
            .GetProperty("get")
            .GetProperty("security");
        var requirement = branchSecurity[0];

        Assert.True(requirement.TryGetProperty("authenticationCookie", out _));
        Assert.True(requirement.TryGetProperty("sessionCookie", out _));

        var protectedOperations = new[]
        {
            (Path: "/api/branches/{branchId}/users", Method: "get"),
            (Path: "/api/branches/{branchId}/users", Method: "post"),
            (Path: "/api/branches/{branchId}/users/{userId}", Method: "delete"),
            (Path: "/api/me/branches", Method: "get"),
            (Path: "/api/me/branch-context", Method: "get"),
            (Path: "/api/me/branch-context", Method: "post"),
            (Path: "/api/v1/suppliers", Method: "get"),
            (Path: "/api/v1/suppliers", Method: "post"),
            (Path: "/api/v1/suppliers/{supplierId}", Method: "get"),
            (Path: "/api/v1/suppliers/{supplierId}", Method: "put"),
            (Path: "/api/v1/suppliers/{supplierId}", Method: "delete"),
            (Path: "/api/v1/purchase-orders", Method: "get"),
            (Path: "/api/v1/purchase-orders", Method: "post"),
            (Path: "/api/v1/purchase-orders/{purchaseOrderId}", Method: "get"),
            (Path: "/api/v1/purchase-orders/{purchaseOrderId}", Method: "put"),
            (Path: "/api/v1/purchase-orders/{purchaseOrderId}/submit", Method: "post"),
            (Path: "/api/v1/purchase-orders/{purchaseOrderId}/cancel", Method: "post"),
            (Path: "/api/v1/purchase-orders/{purchaseOrderId}/lines", Method: "get"),
            (Path: "/api/v1/purchase-orders/{purchaseOrderId}/lines", Method: "post"),
            (Path: "/api/v1/purchase-orders/{purchaseOrderId}/lines/{lineId}", Method: "put"),
            (Path: "/api/v1/purchase-orders/{purchaseOrderId}/lines/{lineId}", Method: "delete")
            ,(Path: "/api/v1/purchase-orders/{purchaseOrderId}/receipts", Method: "get")
            ,(Path: "/api/v1/purchase-orders/{purchaseOrderId}/receipts", Method: "post")
            ,(Path: "/api/v1/goods-receipts/{receiptId}", Method: "get")
        };
        foreach (var protectedOperation in protectedOperations)
        {
            var securityRequirement = document.RootElement
                .GetProperty("paths")
                .GetProperty(protectedOperation.Path)
                .GetProperty(protectedOperation.Method)
                .GetProperty("security")[0];
            Assert.True(securityRequirement.TryGetProperty("authenticationCookie", out _));
            Assert.True(securityRequirement.TryGetProperty("sessionCookie", out _));
        }

        var supplierOperations = document.RootElement.GetProperty("paths");
        Assert.True(supplierOperations.GetProperty("/api/v1/suppliers").GetProperty("get")
            .GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(supplierOperations.GetProperty("/api/v1/suppliers").GetProperty("post")
            .GetProperty("responses").TryGetProperty("201", out _));
        var supplierRequest = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("SupplierUpsertRequest").GetProperty("properties");
        Assert.True(supplierRequest.TryGetProperty("code", out _));
        Assert.True(supplierRequest.TryGetProperty("name", out _));
        Assert.False(supplierRequest.TryGetProperty("organizationId", out _));

        var purchaseOrderPaths = document.RootElement.GetProperty("paths");
        var purchaseOrderRequest = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("PurchaseOrderUpsertRequest").GetProperty("properties");
        Assert.True(purchaseOrderRequest.TryGetProperty("supplierId", out _));
        Assert.True(purchaseOrderRequest.TryGetProperty("branchId", out _));
        Assert.False(purchaseOrderRequest.TryGetProperty("organizationId", out _));
        Assert.True(purchaseOrderPaths.GetProperty("/api/v1/purchase-orders").GetProperty("post")
            .GetProperty("responses").TryGetProperty("201", out _));
        Assert.False(purchaseOrderPaths.GetProperty("/api/v1/purchase-orders/{purchaseOrderId}").TryGetProperty("delete", out _));

        var linePaths = document.RootElement.GetProperty("paths");
        Assert.True(linePaths.GetProperty("/api/v1/purchase-orders/{purchaseOrderId}/lines").GetProperty("post")
            .GetProperty("responses").TryGetProperty("201", out _));
        Assert.True(linePaths.GetProperty("/api/v1/purchase-orders/{purchaseOrderId}/lines/{lineId}").GetProperty("delete")
            .GetProperty("responses").TryGetProperty("204", out _));
        var lineRequest = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("PurchaseOrderLineUpsertRequest").GetProperty("properties");
        Assert.True(lineRequest.TryGetProperty("productId", out _));
        Assert.True(lineRequest.TryGetProperty("quantity", out _));
        Assert.True(lineRequest.TryGetProperty("unitCost", out _));
        Assert.False(lineRequest.TryGetProperty("organizationId", out _));
        Assert.False(lineRequest.TryGetProperty("purchaseOrderId", out _));
        var lineResponse = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("PurchaseOrderLineResponse").GetProperty("properties");
        Assert.True(lineResponse.TryGetProperty("quantity", out _));
        Assert.True(lineResponse.TryGetProperty("receivedQuantity", out _));
        Assert.True(lineResponse.TryGetProperty("remainingQuantity", out _));
        Assert.True(lineResponse.TryGetProperty("isFullyReceived", out _));
        var receivingSummaryPath = document.RootElement.GetProperty("paths")
            .GetProperty("/api/v1/purchase-orders/{purchaseOrderId}/receiving-summary").GetProperty("get");
        Assert.True(receivingSummaryPath.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(receivingSummaryPath.GetProperty("security")[0].TryGetProperty("authenticationCookie", out _));
        Assert.True(receivingSummaryPath.GetProperty("security")[0].TryGetProperty("sessionCookie", out _));
        var receivingSummaryResponse = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("PurchaseOrderReceivingSummaryResponse").GetProperty("properties");
        Assert.True(receivingSummaryResponse.TryGetProperty("receivingState", out _));
        Assert.True(receivingSummaryResponse.TryGetProperty("orderedQuantity", out _));
        Assert.True(receivingSummaryResponse.TryGetProperty("receivedQuantity", out _));
        Assert.True(receivingSummaryResponse.TryGetProperty("remainingQuantity", out _));
        Assert.True(receivingSummaryResponse.TryGetProperty("lines", out _));

        Assert.False(document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/auth/login")
            .GetProperty("post")
            .TryGetProperty("security", out _));

        var receiptPaths = document.RootElement.GetProperty("paths");
        Assert.True(receiptPaths.GetProperty("/api/v1/purchase-orders/{purchaseOrderId}/receipts").GetProperty("post")
            .GetProperty("responses").TryGetProperty("201", out _));
        Assert.True(receiptPaths.GetProperty("/api/v1/goods-receipts/{receiptId}").GetProperty("get")
            .GetProperty("responses").TryGetProperty("200", out _));
        var receiptRequest = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("CreateGoodsReceiptRequest").GetProperty("properties");
        Assert.True(receiptRequest.TryGetProperty("lines", out _));
        Assert.False(receiptRequest.TryGetProperty("branchId", out _));
        Assert.False(receiptRequest.TryGetProperty("organizationId", out _));

        using var anonymousList = await client.GetAsync("/api/v1/suppliers");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousList.StatusCode);
        using var anonymousCreate = await client.PostAsJsonAsync("/api/v1/suppliers", new
        {
            code = "SUP-001",
            name = "Supplier"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousCreate.StatusCode);

        using var anonymousPurchaseOrderList = await client.GetAsync("/api/v1/purchase-orders");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPurchaseOrderList.StatusCode);
        using var anonymousPurchaseOrderDetail = await client.GetAsync($"/api/v1/purchase-orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPurchaseOrderDetail.StatusCode);
        using var anonymousPurchaseOrderCreate = await client.PostAsJsonAsync("/api/v1/purchase-orders", new
        {
            supplierId = Guid.NewGuid(),
            branchId = Guid.NewGuid(),
            orderDate = DateTimeOffset.UtcNow,
            expectedDate = (DateTimeOffset?)null,
            notes = "anonymous request"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPurchaseOrderCreate.StatusCode);
        using var anonymousPurchaseOrderLineList = await client.GetAsync($"/api/v1/purchase-orders/{Guid.NewGuid()}/lines");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPurchaseOrderLineList.StatusCode);
        using var anonymousReceivingSummary = await client.GetAsync($"/api/v1/purchase-orders/{Guid.NewGuid()}/receiving-summary");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousReceivingSummary.StatusCode);
        var lineBody = new { productId = Guid.NewGuid(), quantity = 1m, unitCost = 2m, notes = (string?)null };
        using var anonymousPurchaseOrderLineCreate = await client.PostAsJsonAsync(
            $"/api/v1/purchase-orders/{Guid.NewGuid()}/lines", lineBody);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPurchaseOrderLineCreate.StatusCode);
        using var anonymousPurchaseOrderLineUpdate = await client.PutAsJsonAsync(
            $"/api/v1/purchase-orders/{Guid.NewGuid()}/lines/{Guid.NewGuid()}", lineBody);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPurchaseOrderLineUpdate.StatusCode);
        using var anonymousPurchaseOrderLineDelete = await client.DeleteAsync(
            $"/api/v1/purchase-orders/{Guid.NewGuid()}/lines/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPurchaseOrderLineDelete.StatusCode);
        using var anonymousReceiptList = await client.GetAsync($"/api/v1/purchase-orders/{Guid.NewGuid()}/receipts");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousReceiptList.StatusCode);
        using var anonymousReceiptCreate = await client.PostAsJsonAsync($"/api/v1/purchase-orders/{Guid.NewGuid()}/receipts",
            new { receivedAt = DateTimeOffset.UtcNow, lines = new[] { new { purchaseOrderLineId = Guid.NewGuid(), quantityReceived = 1m } } });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousReceiptCreate.StatusCode);
        using var anonymousReceiptDetail = await client.GetAsync($"/api/v1/goods-receipts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousReceiptDetail.StatusCode);
    }
}

[Collection("PostgreSQL integration")]
public class IdentityPersistenceTests
{
    [PostgresFact]
    public async Task IdentityData_PersistsWithUniqueConstraintsAndTenantIsolation()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        var options = new DbContextOptionsBuilder<SmartShopPosDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var dbContext = new SmartShopPosDbContext(options);
        await dbContext.Database.MigrateAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var organizationA = new Organization("Test organization A", $"test-{Guid.NewGuid():N}");
        var organizationB = new Organization("Test organization B", $"test-{Guid.NewGuid():N}");
        var alice = new User(organizationA.Id, "Alice@example.test", "Alice", "test-hash");
        var bob = new User(organizationB.Id, "Bob@example.test", "Bob", "test-hash");
        var cashierA = new Role(organizationA.Id, "Cashier");
        var cashierB = new Role(organizationB.Id, "Cashier");

        dbContext.AddRange(organizationA, organizationB, alice, bob, cashierA, cashierB);
        await dbContext.SaveChangesAsync();

        var createSales = await dbContext.Permissions.SingleAsync(permission => permission.Key == "sales.create");
        var viewReports = await dbContext.Permissions.SingleAsync(permission => permission.Key == "reports.view");
        var aliceAssignment = new UserRole(organizationA.Id, alice.Id, cashierA.Id);
        var bobAssignment = new UserRole(organizationB.Id, bob.Id, cashierB.Id);

        dbContext.AddRange(
            aliceAssignment,
            bobAssignment,
            new RolePermission(cashierA.Id, createSales.Id),
            new RolePermission(cashierB.Id, viewReports.Id));
        await dbContext.SaveChangesAsync();

        var permissionChecker = new EfCorePermissionChecker(dbContext);
        Assert.True(await permissionChecker.HasPermissionAsync(alice.Id, "SALES.CREATE"));
        Assert.False(await permissionChecker.HasPermissionAsync(alice.Id, "reports.view"));
        Assert.True(await permissionChecker.HasPermissionAsync(bob.Id, "reports.view"));
        Assert.False(await permissionChecker.HasPermissionAsync(bob.Id, "sales.create"));

        await AssertDatabaseRejectsAsync(dbContext, new Organization("Duplicate", organizationA.Code));
        await AssertDatabaseRejectsAsync(dbContext, new User(organizationA.Id, "ALICE@EXAMPLE.TEST", "Duplicate", "test-hash"));
        await AssertDatabaseRejectsAsync(dbContext, new Role(organizationA.Id, "cashier"));
        await AssertDatabaseRejectsAsync(dbContext, new Permission("sales.create", "Duplicate"));
        await AssertDatabaseRejectsAsync(dbContext, new UserRole(organizationA.Id, alice.Id, cashierA.Id));
        await AssertDatabaseRejectsAsync(dbContext, new RolePermission(cashierA.Id, createSales.Id));
        await AssertDatabaseRejectsAsync(dbContext, new UserRole(organizationA.Id, alice.Id, cashierB.Id));

        dbContext.ChangeTracker.Clear();
        Assert.Equal(2, await dbContext.Organizations.CountAsync(organization =>
            organization.Id == organizationA.Id || organization.Id == organizationB.Id));
    }

    private static async Task AssertDatabaseRejectsAsync<TEntity>(SmartShopPosDbContext dbContext, TEntity entity)
        where TEntity : class
    {
        dbContext.Add(entity);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        dbContext.ChangeTracker.Clear();
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")))
        {
            Skip = "Set ConnectionStrings__DefaultConnection to run PostgreSQL integration tests.";
        }
    }
}

[CollectionDefinition("PostgreSQL integration", DisableParallelization = true)]
public sealed class PostgreSqlIntegrationCollection;
