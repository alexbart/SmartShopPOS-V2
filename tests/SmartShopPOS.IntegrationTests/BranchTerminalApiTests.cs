using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using SmartShopPOS.Contracts.Authentication;
using SmartShopPOS.Contracts.Branches;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.IntegrationTests;

[Collection("PostgreSQL integration")]
public sealed class BranchTerminalApiTests
{
    private const string TestPassword = "Branch-terminal-test-password!";

    [PostgresFact]
    public async Task BranchAndTerminalEndpoints_EnforcePermissionsTenantIsolationAndDatabaseConstraints()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")!;
        var organizationA = new Organization("Branch test organization A", $"branch-a-{Guid.NewGuid():N}");
        var organizationB = new Organization("Branch test organization B", $"branch-b-{Guid.NewGuid():N}");
        var userA = new User(organizationA.Id, $"branch-a-{Guid.NewGuid():N}@example.test", "Branch user A",
            new Pbkdf2PasswordHasher().HashPassword(TestPassword));
        var unauthorizedUser = new User(organizationA.Id, $"branch-denied-{Guid.NewGuid():N}@example.test", "Branch user denied",
            new Pbkdf2PasswordHasher().HashPassword(TestPassword));
        var userB = new User(organizationB.Id, $"branch-b-{Guid.NewGuid():N}@example.test", "Branch user B",
            new Pbkdf2PasswordHasher().HashPassword(TestPassword));
        var roleA = new Role(organizationA.Id, $"branch-role-{Guid.NewGuid():N}");
        var roleB = new Role(organizationB.Id, $"branch-role-{Guid.NewGuid():N}");
        var options = new DbContextOptionsBuilder<SmartShopPosDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using (var setupContext = new SmartShopPosDbContext(options))
        {
            await setupContext.Database.MigrateAsync();
            setupContext.AddRange(organizationA, organizationB, userA, unauthorizedUser, userB, roleA, roleB);
            await setupContext.SaveChangesAsync();

            var permissions = await setupContext.Permissions
                .Where(permission => new[] { "branches.view", "branches.create", "terminals.view", "terminals.create" }
                    .Contains(permission.Key))
                .ToListAsync();
            var permissionIds = permissions.Select(permission => permission.Id).ToArray();
            setupContext.AddRange(
                new UserRole(organizationA.Id, userA.Id, roleA.Id),
                new UserRole(organizationB.Id, userB.Id, roleB.Id));
            setupContext.AddRange(permissionIds.Select(permissionId => new RolePermission(roleA.Id, permissionId)));
            setupContext.AddRange(permissionIds.Select(permissionId => new RolePermission(roleB.Id, permissionId)));
            await setupContext.SaveChangesAsync();
        }

        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
            {
                webHost.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
                webHost.ConfigureServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
            });
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = false,
                AllowAutoRedirect = false
            });

            using (var anonymousResponse = await SendAsync(client, HttpMethod.Get, "/api/branches"))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
            }

            var deniedCookies = await LoginAsync(client, unauthorizedUser.Email, organizationA.Code);
            using (var deniedResponse = await SendAsync(client, HttpMethod.Get, "/api/branches", deniedCookies))
            {
                Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
            }

            var cookiesA = await LoginAsync(client, userA.Email, organizationA.Code);
            var cookiesB = await LoginAsync(client, userB.Email, organizationB.Code);
            using var spoofedTenantResponse = await SendAsync(
                client,
                HttpMethod.Post,
                "/api/branches",
                cookiesA,
                new { code = "nai-001", name = "Nairobi CBD", organizationId = organizationB.Id });
            Assert.Equal(HttpStatusCode.Created, spoofedTenantResponse.StatusCode);
            var branchA = await spoofedTenantResponse.Content.ReadFromJsonAsync<BranchResponse>();
            Assert.NotNull(branchA);
            Assert.Equal("NAI-001", branchA.Code);

            using (var duplicateBranchResponse = await SendAsync(
                       client, HttpMethod.Post, "/api/branches", cookiesA,
                       new CreateBranchRequest("nai-001", "Duplicate Nairobi")))
            {
                Assert.Equal(HttpStatusCode.Conflict, duplicateBranchResponse.StatusCode);
            }

            using var branchBResponse = await SendAsync(
                client, HttpMethod.Post, "/api/branches", cookiesB,
                new CreateBranchRequest("NAI-001", "Nairobi in organization B"));
            Assert.Equal(HttpStatusCode.Created, branchBResponse.StatusCode);
            var branchB = await branchBResponse.Content.ReadFromJsonAsync<BranchResponse>();
            Assert.NotNull(branchB);

            using (var listBranchesResponse = await SendAsync(client, HttpMethod.Get, "/api/branches", cookiesA))
            {
                Assert.Equal(HttpStatusCode.OK, listBranchesResponse.StatusCode);
                var branches = await listBranchesResponse.Content.ReadFromJsonAsync<List<BranchResponse>>();
                Assert.NotNull(branches);
                Assert.Single(branches);
                Assert.Equal(branchA.Id, branches[0].Id);
            }

            using var secondBranchResponse = await SendAsync(
                client, HttpMethod.Post, "/api/branches", cookiesA,
                new CreateBranchRequest("NAI-002", "Nairobi Westlands"));
            Assert.Equal(HttpStatusCode.Created, secondBranchResponse.StatusCode);
            var secondBranch = await secondBranchResponse.Content.ReadFromJsonAsync<BranchResponse>();
            Assert.NotNull(secondBranch);

            using var terminalResponse = await SendAsync(
                client, HttpMethod.Post, $"/api/branches/{branchA.Id}/terminals", cookiesA,
                new { code = "pos-01", name = "Counter 1", organizationId = organizationB.Id, branchId = branchB.Id });
            Assert.Equal(HttpStatusCode.Created, terminalResponse.StatusCode);
            var terminal = await terminalResponse.Content.ReadFromJsonAsync<TerminalResponse>();
            Assert.NotNull(terminal);
            Assert.Equal(branchA.Id, terminal.BranchId);
            Assert.Equal("POS-01", terminal.Code);

            using (var duplicateTerminalResponse = await SendAsync(
                       client, HttpMethod.Post, $"/api/branches/{branchA.Id}/terminals", cookiesA,
                       new CreateTerminalRequest("POS-01", "Duplicate Counter")))
            {
                Assert.Equal(HttpStatusCode.Conflict, duplicateTerminalResponse.StatusCode);
            }

            using var sameTerminalCodeOtherBranch = await SendAsync(
                client, HttpMethod.Post, $"/api/branches/{secondBranch.Id}/terminals", cookiesA,
                new CreateTerminalRequest("POS-01", "Westlands Counter 1"));
            Assert.Equal(HttpStatusCode.Created, sameTerminalCodeOtherBranch.StatusCode);

            using (var crossTenantCreateResponse = await SendAsync(
                       client, HttpMethod.Post, $"/api/branches/{branchB.Id}/terminals", cookiesA,
                       new CreateTerminalRequest("POS-02", "Foreign branch terminal")))
            {
                Assert.Equal(HttpStatusCode.NotFound, crossTenantCreateResponse.StatusCode);
            }

            using (var crossTenantListResponse = await SendAsync(
                       client, HttpMethod.Get, $"/api/branches/{branchB.Id}/terminals", cookiesA))
            {
                Assert.Equal(HttpStatusCode.NotFound, crossTenantListResponse.StatusCode);
            }

            using (var listTerminalsResponse = await SendAsync(
                       client, HttpMethod.Get, $"/api/branches/{branchA.Id}/terminals", cookiesA))
            {
                Assert.Equal(HttpStatusCode.OK, listTerminalsResponse.StatusCode);
                var terminals = await listTerminalsResponse.Content.ReadFromJsonAsync<List<TerminalResponse>>();
                Assert.NotNull(terminals);
                Assert.Single(terminals);
                Assert.Equal(terminal.Id, terminals[0].Id);
            }

            await using (var deactivateContext = new SmartShopPosDbContext(options))
            {
                await deactivateContext.Branches
                    .Where(branch => branch.Id == secondBranch.Id)
                    .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.IsActive, false));
            }

            using (var inactiveBranchResponse = await SendAsync(
                       client, HttpMethod.Post, $"/api/branches/{secondBranch.Id}/terminals", cookiesA,
                       new CreateTerminalRequest("POS-02", "Inactive branch terminal")))
            {
                Assert.Equal(HttpStatusCode.Conflict, inactiveBranchResponse.StatusCode);
            }

            await using (var constraintContext = new SmartShopPosDbContext(options))
            {
                constraintContext.Terminals.Add(new Terminal(
                    organizationB.Id,
                    branchA.Id,
                    $"DB-{Guid.NewGuid():N}"[..11],
                    "Cross-organization foreign key probe"));
                await Assert.ThrowsAsync<DbUpdateException>(() => constraintContext.SaveChangesAsync());
            }

            await using (var duplicateBranchContext = new SmartShopPosDbContext(options))
            {
                duplicateBranchContext.Branches.Add(new Branch(organizationA.Id, "NAI-001", "Database duplicate"));
                await Assert.ThrowsAsync<DbUpdateException>(() => duplicateBranchContext.SaveChangesAsync());
            }

            await using (var duplicateTerminalContext = new SmartShopPosDbContext(options))
            {
                duplicateTerminalContext.Terminals.Add(new Terminal(organizationA.Id, branchA.Id, "POS-01", "Database duplicate"));
                await Assert.ThrowsAsync<DbUpdateException>(() => duplicateTerminalContext.SaveChangesAsync());
            }

            await using var resultContext = new SmartShopPosDbContext(options);
            var persistedSpoofedBranch = await resultContext.Branches.SingleAsync(branch => branch.Id == branchA.Id);
            Assert.Equal(organizationA.Id, persistedSpoofedBranch.OrganizationId);
            var persistedOrganizationBBranch = await resultContext.Branches.SingleAsync(branch => branch.Id == branchB.Id);
            Assert.Equal(organizationB.Id, persistedOrganizationBBranch.OrganizationId);
            var persistedTerminal = await resultContext.Terminals.SingleAsync(existingTerminal => existingTerminal.Id == terminal.Id);
            Assert.Equal(organizationA.Id, persistedTerminal.OrganizationId);
            Assert.Equal(branchA.Id, persistedTerminal.BranchId);
        }
        finally
        {
            await using var cleanupContext = new SmartShopPosDbContext(options);
            var organizationIds = new[] { organizationA.Id, organizationB.Id };
            cleanupContext.Terminals.RemoveRange(await cleanupContext.Terminals
                .Where(terminal => organizationIds.Contains(terminal.OrganizationId)).ToListAsync());
            cleanupContext.Branches.RemoveRange(await cleanupContext.Branches
                .Where(branch => organizationIds.Contains(branch.OrganizationId)).ToListAsync());
            cleanupContext.AuthenticationSessions.RemoveRange(await cleanupContext.AuthenticationSessions
                .Where(session => organizationIds.Contains(session.OrganizationId)).ToListAsync());
            cleanupContext.UserRoles.RemoveRange(await cleanupContext.UserRoles
                .Where(userRole => organizationIds.Contains(userRole.OrganizationId)).ToListAsync());
            cleanupContext.Roles.RemoveRange(await cleanupContext.Roles
                .Where(role => organizationIds.Contains(role.OrganizationId)).ToListAsync());
            cleanupContext.Users.RemoveRange(await cleanupContext.Users
                .Where(user => organizationIds.Contains(user.OrganizationId)).ToListAsync());
            cleanupContext.Organizations.RemoveRange(await cleanupContext.Organizations
                .Where(organization => organizationIds.Contains(organization.Id)).ToListAsync());
            await cleanupContext.SaveChangesAsync();
        }
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string organizationCode)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, organizationCode, TestPassword));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return string.Join("; ", response.Headers.GetValues("Set-Cookie")
            .Select(cookie => cookie.Split(';', 2)[0]));
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string? cookies = null,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(cookies))
        {
            request.Headers.Add("Cookie", cookies);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        return await client.SendAsync(request);
    }
}
