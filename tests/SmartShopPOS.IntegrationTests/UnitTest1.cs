using System.Net;
using System.Text.Json;
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
        Assert.False(document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/auth/login")
            .GetProperty("post")
            .TryGetProperty("security", out _));
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
