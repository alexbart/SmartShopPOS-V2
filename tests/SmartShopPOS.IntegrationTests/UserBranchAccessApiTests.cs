using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartShopPOS.Contracts.Authentication;
using SmartShopPOS.Contracts.UserBranches;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Identity;
using SmartShopPOS.Infrastructure.Persistence;
using SmartShopPOS.Application.UserBranches;

namespace SmartShopPOS.IntegrationTests;

[Collection("PostgreSQL integration")]
public sealed class UserBranchAccessApiTests
{
    private const string TestPassword = "User-branch-access-test-password!";
    private static readonly string[] PermissionKeys =
    [
        "user_branch_assignments.view",
        "user_branch_assignments.create",
        "user_branch_assignments.deactivate",
        "branch_context.select"
    ];

    [PostgresFact]
    public async Task UserBranchAssignmentsAndContext_EnforceTenantAssignmentPermissionAndSessionBoundaries()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")!;
        var organizationA = new Organization("User branch organization A", $"user-branch-a-{Guid.NewGuid():N}");
        var organizationB = new Organization("User branch organization B", $"user-branch-b-{Guid.NewGuid():N}");
        var branchA = new Branch(organizationA.Id, "A-001", "Branch A");
        var branchA2 = new Branch(organizationA.Id, "A-002", "Branch A2");
        var inactiveBranchA = new Branch(organizationA.Id, "A-003", "Inactive branch A");
        inactiveBranchA.SetActive(false);
        var branchB = new Branch(organizationB.Id, "B-001", "Branch B");
        var admin = CreateUser(organizationA.Id, "admin");
        var operatorUser = CreateUser(organizationA.Id, "operator");
        var unassignedUser = CreateUser(organizationA.Id, "unassigned");
        var soonInactiveUser = CreateUser(organizationA.Id, "inactive");
        var foreignUser = CreateUser(organizationB.Id, "foreign");
        var adminRole = new Role(organizationA.Id, $"user-branch-admin-{Guid.NewGuid():N}");
        var operatorRole = new Role(organizationA.Id, $"user-branch-operator-{Guid.NewGuid():N}");
        var unassignedRole = new Role(organizationA.Id, $"user-branch-unassigned-{Guid.NewGuid():N}");
        var inactiveRole = new Role(organizationA.Id, $"user-branch-inactive-{Guid.NewGuid():N}");
        var foreignRole = new Role(organizationB.Id, $"user-branch-foreign-{Guid.NewGuid():N}");
        var options = new DbContextOptionsBuilder<SmartShopPosDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using (var setupContext = new SmartShopPosDbContext(options))
        {
            await setupContext.Database.MigrateAsync();
            setupContext.AddRange(
                organizationA, organizationB, branchA, branchA2, inactiveBranchA, branchB,
                admin, operatorUser, unassignedUser, soonInactiveUser, foreignUser,
                adminRole, operatorRole, unassignedRole, inactiveRole, foreignRole);
            await setupContext.SaveChangesAsync();

            var permissions = await setupContext.Permissions
                .Where(permission => PermissionKeys.Contains(permission.Key))
                .ToDictionaryAsync(permission => permission.Key, cancellationToken: default);
            Assert.Equal(PermissionKeys.Length, permissions.Count);

            setupContext.AddRange(
                new UserRole(organizationA.Id, admin.Id, adminRole.Id),
                new UserRole(organizationA.Id, operatorUser.Id, operatorRole.Id),
                new UserRole(organizationA.Id, unassignedUser.Id, unassignedRole.Id),
                new UserRole(organizationA.Id, soonInactiveUser.Id, inactiveRole.Id),
                new UserRole(organizationB.Id, foreignUser.Id, foreignRole.Id));
            setupContext.AddRange(PermissionKeys.Select(key => new RolePermission(adminRole.Id, permissions[key].Id)));
            setupContext.Add(new RolePermission(operatorRole.Id, permissions["branch_context.select"].Id));
            setupContext.Add(new RolePermission(unassignedRole.Id, permissions["branch_context.select"].Id));
            setupContext.Add(new RolePermission(inactiveRole.Id, permissions["branch_context.select"].Id));
            await setupContext.SaveChangesAsync();
        }

        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
                webHost.UseSetting("ConnectionStrings:DefaultConnection", connectionString));
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = false,
                AllowAutoRedirect = false
            });

            using (var anonymousBranches = await SendAsync(client, HttpMethod.Get, "/api/me/branches"))
            using (var anonymousContext = await SendAsync(client, HttpMethod.Get, "/api/me/branch-context"))
            using (var anonymousAssignments = await SendAsync(client, HttpMethod.Get, $"/api/branches/{branchA.Id}/users"))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymousBranches.StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, anonymousContext.StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, anonymousAssignments.StatusCode);
            }

            var adminCookies = await LoginAsync(client, admin.Email, organizationA.Code);
            using (var invalidUserAssignment = await SendAsync(
                       client,
                       HttpMethod.Post,
                       $"/api/branches/{branchA.Id}/users",
                       adminCookies,
                       new AssignUserToBranchRequest(Guid.Empty)))
            {
                Assert.Equal(HttpStatusCode.BadRequest, invalidUserAssignment.StatusCode);
            }

            using (var foreignBranchAssignments = await SendAsync(
                       client, HttpMethod.Get, $"/api/branches/{branchB.Id}/users", adminCookies))
            {
                Assert.Equal(HttpStatusCode.NotFound, foreignBranchAssignments.StatusCode);
            }

            using (var foreignUserAssignment = await SendAsync(
                       client,
                       HttpMethod.Post,
                       $"/api/branches/{branchA.Id}/users",
                       adminCookies,
                       new AssignUserToBranchRequest(foreignUser.Id)))
            {
                Assert.Equal(HttpStatusCode.NotFound, foreignUserAssignment.StatusCode);
            }

            using var operatorAssignmentResponse = await SendAsync(
                client,
                HttpMethod.Post,
                $"/api/branches/{branchA.Id}/users",
                adminCookies,
                new AssignUserToBranchRequest(operatorUser.Id));
            Assert.Equal(HttpStatusCode.Created, operatorAssignmentResponse.StatusCode);
            var operatorAssignment = await operatorAssignmentResponse.Content.ReadFromJsonAsync<UserBranchAssignmentResponse>();
            Assert.NotNull(operatorAssignment);

            using (var duplicateAssignment = await SendAsync(
                       client,
                       HttpMethod.Post,
                       $"/api/branches/{branchA.Id}/users",
                       adminCookies,
                       new AssignUserToBranchRequest(operatorUser.Id)))
            {
                Assert.Equal(HttpStatusCode.Conflict, duplicateAssignment.StatusCode);
            }

            using var secondBranchAssignment = await SendAsync(
                client,
                HttpMethod.Post,
                $"/api/branches/{branchA2.Id}/users",
                adminCookies,
                new AssignUserToBranchRequest(operatorUser.Id));
            Assert.Equal(HttpStatusCode.Created, secondBranchAssignment.StatusCode);

            using (var inactiveBranchAssignment = await SendAsync(
                       client,
                       HttpMethod.Post,
                       $"/api/branches/{inactiveBranchA.Id}/users",
                       adminCookies,
                       new AssignUserToBranchRequest(operatorUser.Id)))
            {
                Assert.Equal(HttpStatusCode.Conflict, inactiveBranchAssignment.StatusCode);
            }

            using var soonInactiveAssignment = await SendAsync(
                client,
                HttpMethod.Post,
                $"/api/branches/{branchA.Id}/users",
                adminCookies,
                new AssignUserToBranchRequest(soonInactiveUser.Id));
            Assert.Equal(HttpStatusCode.Created, soonInactiveAssignment.StatusCode);

            using (var assignmentList = await SendAsync(
                       client, HttpMethod.Get, $"/api/branches/{branchA.Id}/users", adminCookies))
            {
                Assert.Equal(HttpStatusCode.OK, assignmentList.StatusCode);
                var assignments = await assignmentList.Content.ReadFromJsonAsync<List<UserBranchAssignmentResponse>>();
                Assert.NotNull(assignments);
                Assert.Equal(2, assignments.Count);
                Assert.All(assignments, assignment => Assert.True(assignment.IsActive));
            }

            var operatorCookies = await LoginAsync(client, operatorUser.Email, organizationA.Code);
            Guid operatorSessionId;
            await using (var sessionContext = new SmartShopPosDbContext(options))
            {
                operatorSessionId = await sessionContext.AuthenticationSessions
                    .Where(session => session.UserId == operatorUser.Id && session.RevokedAt == null)
                    .Select(session => session.SessionId)
                    .SingleAsync();
            }

            using (var listMyBranches = await SendAsync(client, HttpMethod.Get, "/api/me/branches", operatorCookies))
            {
                Assert.Equal(HttpStatusCode.OK, listMyBranches.StatusCode);
                var accessibleBranches = await listMyBranches.Content.ReadFromJsonAsync<List<AccessibleBranchResponse>>();
                Assert.NotNull(accessibleBranches);
                Assert.Equal(new[] { branchA.Id, branchA2.Id }.Order(), accessibleBranches.Select(branch => branch.Id).Order());
            }

            using (var emptyContext = await SendAsync(client, HttpMethod.Get, "/api/me/branch-context", operatorCookies))
            {
                Assert.Equal(HttpStatusCode.NoContent, emptyContext.StatusCode);
            }

            using (var invalidContext = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/me/branch-context",
                       operatorCookies,
                       new SelectBranchContextRequest(Guid.Empty)))
            {
                Assert.Equal(HttpStatusCode.BadRequest, invalidContext.StatusCode);
            }

            using var selectedBranchResponse = await SendAsync(
                client,
                HttpMethod.Post,
                "/api/me/branch-context",
                operatorCookies,
                new { branchId = branchA.Id, organizationId = organizationB.Id });
            Assert.Equal(HttpStatusCode.OK, selectedBranchResponse.StatusCode);
            var selectedBranch = await selectedBranchResponse.Content.ReadFromJsonAsync<BranchContextResponse>();
            Assert.NotNull(selectedBranch);
            Assert.Equal(branchA.Id, selectedBranch.BranchId);
            Assert.DoesNotContain("branch", operatorCookies, StringComparison.OrdinalIgnoreCase);

            using (var authenticatedUserResponse = await SendAsync(client, HttpMethod.Get, "/api/auth/me", operatorCookies))
            {
                Assert.Equal(HttpStatusCode.OK, authenticatedUserResponse.StatusCode);
                var authenticatedUser = await authenticatedUserResponse.Content.ReadFromJsonAsync<AuthenticatedUserResponse>();
                Assert.NotNull(authenticatedUser);
                Assert.Equal(organizationA.Id, authenticatedUser.OrganizationId);
                Assert.Equal(operatorUser.Id, authenticatedUser.Id);
            }

            using (var currentContext = await SendAsync(
                       client,
                       HttpMethod.Get,
                       "/api/me/branch-context?organizationId=" + organizationB.Id,
                       operatorCookies))
            {
                Assert.Equal(HttpStatusCode.OK, currentContext.StatusCode);
                var context = await currentContext.Content.ReadFromJsonAsync<BranchContextResponse>();
                Assert.NotNull(context);
                Assert.Equal(branchA.Id, context.BranchId);
            }

            using (var accessScope = factory.Services.CreateScope())
            {
                var accessHttpContextAccessor = accessScope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
                accessHttpContextAccessor.HttpContext = CreateAuthenticatedContext(operatorUser.Id, organizationA.Id, operatorSessionId);
                var accessService = accessScope.ServiceProvider.GetRequiredService<IBranchAccessService>();
                Assert.True(await accessService.CanOperateInBranchAsync(branchA.Id, "branch_context.select"));
                Assert.False(await accessService.CanOperateInBranchAsync(branchA2.Id, "branch_context.select"));
                Assert.False(await accessService.CanOperateInBranchAsync(branchA.Id, "branches.view"));
            }

            var unassignedCookies = await LoginAsync(client, unassignedUser.Email, organizationA.Code);
            using (var unassignedBranches = await SendAsync(client, HttpMethod.Get, "/api/me/branches", unassignedCookies))
            {
                Assert.Equal(HttpStatusCode.OK, unassignedBranches.StatusCode);
                Assert.Empty(await unassignedBranches.Content.ReadFromJsonAsync<List<AccessibleBranchResponse>>() ?? []);
            }

            using (var unassignedSelection = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/me/branch-context",
                       unassignedCookies,
                       new SelectBranchContextRequest(branchA.Id)))
            {
                Assert.Equal(HttpStatusCode.Forbidden, unassignedSelection.StatusCode);
            }

            using (var foreignSelection = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/me/branch-context",
                       operatorCookies,
                       new SelectBranchContextRequest(branchB.Id)))
            {
                Assert.Equal(HttpStatusCode.NotFound, foreignSelection.StatusCode);
            }

            using (var inactiveSelection = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/me/branch-context",
                       operatorCookies,
                       new SelectBranchContextRequest(inactiveBranchA.Id)))
            {
                Assert.Equal(HttpStatusCode.Conflict, inactiveSelection.StatusCode);
            }

            using (var operatorAssignmentAttempt = await SendAsync(
                       client,
                       HttpMethod.Post,
                       $"/api/branches/{branchA.Id}/users",
                       operatorCookies,
                       new AssignUserToBranchRequest(unassignedUser.Id)))
            {
                Assert.Equal(HttpStatusCode.Forbidden, operatorAssignmentAttempt.StatusCode);
            }

            using (var switchContext = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/me/branch-context",
                       operatorCookies,
                       new SelectBranchContextRequest(branchA2.Id)))
            {
                Assert.Equal(HttpStatusCode.OK, switchContext.StatusCode);
            }

            using (var switchedContext = await SendAsync(client, HttpMethod.Get, "/api/me/branch-context", operatorCookies))
            {
                var context = await switchedContext.Content.ReadFromJsonAsync<BranchContextResponse>();
                Assert.Equal(branchA2.Id, context!.BranchId);
            }

            using (var restoreContext = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/me/branch-context",
                       operatorCookies,
                       new SelectBranchContextRequest(branchA.Id)))
            {
                Assert.Equal(HttpStatusCode.OK, restoreContext.StatusCode);
            }

            using (var deniedAssignments = await SendAsync(
                       client,
                       HttpMethod.Get,
                       $"/api/branches/{branchA.Id}/users",
                       operatorCookies))
            {
                Assert.Equal(HttpStatusCode.Forbidden, deniedAssignments.StatusCode);
            }

            using (var deactivateAssignment = await SendAsync(
                       client,
                       HttpMethod.Delete,
                       $"/api/branches/{branchA.Id}/users/{operatorUser.Id}",
                       adminCookies))
            {
                Assert.Equal(HttpStatusCode.NoContent, deactivateAssignment.StatusCode);
            }

            using (var clearedContext = await SendAsync(client, HttpMethod.Get, "/api/me/branch-context", operatorCookies))
            {
                Assert.Equal(HttpStatusCode.NoContent, clearedContext.StatusCode);
            }

            using var reassignment = await SendAsync(
                client,
                HttpMethod.Post,
                $"/api/branches/{branchA.Id}/users",
                adminCookies,
                new AssignUserToBranchRequest(operatorUser.Id));
            Assert.Equal(HttpStatusCode.Created, reassignment.StatusCode);

            var inactiveUserCookies = await LoginAsync(client, soonInactiveUser.Email, organizationA.Code);
            await using (var updateContext = new SmartShopPosDbContext(options))
            {
                var user = await updateContext.Users.SingleAsync(candidate => candidate.Id == soonInactiveUser.Id);
                user.SetActive(false);
                await updateContext.SaveChangesAsync();
            }

            using (var inactiveUserAssignment = await SendAsync(
                       client,
                       HttpMethod.Post,
                       $"/api/branches/{branchA2.Id}/users",
                       adminCookies,
                       new AssignUserToBranchRequest(soonInactiveUser.Id)))
            {
                Assert.Equal(HttpStatusCode.Conflict, inactiveUserAssignment.StatusCode);
            }

            using (var inactiveUserContext = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/me/branch-context",
                       inactiveUserCookies,
                       new SelectBranchContextRequest(branchA.Id)))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, inactiveUserContext.StatusCode);
            }

            await using (var verificationContext = new SmartShopPosDbContext(options))
            {
                var session = await verificationContext.AuthenticationSessions.SingleAsync(candidate =>
                    candidate.UserId == operatorUser.Id && candidate.RevokedAt == null);
                Assert.Equal(operatorSessionId, session.SessionId);
                Assert.Null(session.SelectedBranchId);

                session.SetSelectedBranch(branchB.Id);
                await Assert.ThrowsAsync<DbUpdateException>(() => verificationContext.SaveChangesAsync());
            }

            await using (var directConstraints = new SmartShopPosDbContext(options))
            {
                directConstraints.UserBranches.Add(new UserBranch(organizationA.Id, foreignUser.Id, branchA.Id));
                await Assert.ThrowsAsync<DbUpdateException>(() => directConstraints.SaveChangesAsync());
            }

            await using (var directDuplicate = new SmartShopPosDbContext(options))
            {
                directDuplicate.UserBranches.Add(new UserBranch(organizationA.Id, operatorUser.Id, branchA2.Id));
                await Assert.ThrowsAsync<DbUpdateException>(() => directDuplicate.SaveChangesAsync());
            }

            using (var reselectContext = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/me/branch-context",
                       operatorCookies,
                       new SelectBranchContextRequest(branchA.Id)))
            {
                Assert.Equal(HttpStatusCode.OK, reselectContext.StatusCode);
            }

            using (var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout"))
            {
                logout.Headers.Add("Cookie", operatorCookies);
                using var logoutResponse = await client.SendAsync(logout);
                Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
            }

            await using (var verificationContext = new SmartShopPosDbContext(options))
            {
                var revokedSession = await verificationContext.AuthenticationSessions.SingleAsync(session => session.SessionId == operatorSessionId);
                Assert.NotNull(revokedSession.RevokedAt);
                Assert.Null(revokedSession.SelectedBranchId);
            }

            using (var revokedContext = await SendAsync(client, HttpMethod.Get, "/api/me/branch-context", operatorCookies))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, revokedContext.StatusCode);
            }

            using var serviceScope = factory.Services.CreateScope();
            var httpContextAccessor = serviceScope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            httpContextAccessor.HttpContext = CreateAuthenticatedContext(operatorUser.Id, organizationA.Id, operatorSessionId);
            var branchAccessService = serviceScope.ServiceProvider.GetRequiredService<IBranchAccessService>();
            Assert.False(await branchAccessService.CanOperateInBranchAsync(branchA.Id, "branch_context.select"));
        }
        finally
        {
            await using var cleanupContext = new SmartShopPosDbContext(options);
            var organizationIds = new[] { organizationA.Id, organizationB.Id };
            cleanupContext.UserBranches.RemoveRange(await cleanupContext.UserBranches
                .Where(assignment => organizationIds.Contains(assignment.OrganizationId)).ToListAsync());
            cleanupContext.AuthenticationSessions.RemoveRange(await cleanupContext.AuthenticationSessions
                .Where(session => organizationIds.Contains(session.OrganizationId)).ToListAsync());
            cleanupContext.UserRoles.RemoveRange(await cleanupContext.UserRoles
                .Where(userRole => organizationIds.Contains(userRole.OrganizationId)).ToListAsync());
            cleanupContext.RolePermissions.RemoveRange(await cleanupContext.RolePermissions
                .Where(assignment => organizationIds.Contains(assignment.Role.OrganizationId)).ToListAsync());
            cleanupContext.Roles.RemoveRange(await cleanupContext.Roles
                .Where(role => organizationIds.Contains(role.OrganizationId)).ToListAsync());
            cleanupContext.Users.RemoveRange(await cleanupContext.Users
                .Where(user => organizationIds.Contains(user.OrganizationId)).ToListAsync());
            cleanupContext.Terminals.RemoveRange(await cleanupContext.Terminals
                .Where(terminal => organizationIds.Contains(terminal.OrganizationId)).ToListAsync());
            cleanupContext.Branches.RemoveRange(await cleanupContext.Branches
                .Where(branch => organizationIds.Contains(branch.OrganizationId)).ToListAsync());
            cleanupContext.Organizations.RemoveRange(await cleanupContext.Organizations
                .Where(organization => organizationIds.Contains(organization.Id)).ToListAsync());
            await cleanupContext.SaveChangesAsync();
        }
    }

    private static User CreateUser(Guid organizationId, string label)
    {
        return new User(
            organizationId,
            $"{label}-{Guid.NewGuid():N}@example.test",
            $"User {label}",
            new Pbkdf2PasswordHasher().HashPassword(TestPassword));
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string organizationCode)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, organizationCode, TestPassword));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.DoesNotContain(cookies, cookie => cookie.StartsWith("branch=", StringComparison.OrdinalIgnoreCase));
        return string.Join("; ", cookies.Select(cookie => cookie.Split(';', 2)[0]));
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

    private static DefaultHttpContext CreateAuthenticatedContext(Guid userId, Guid organizationId, Guid sessionId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("user_id", userId.ToString()),
            new Claim("organization_id", organizationId.ToString()),
            new Claim("session_id", sessionId.ToString())
        ], "integration-test");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }
}