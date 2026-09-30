using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using SmartShopPOS.Contracts.Authentication;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.IntegrationTests;

[Collection("PostgreSQL integration")]
public sealed class AuthenticationFlowTests
{
    [PostgresFact]
    public async Task LoginSessionMe_UsesPersistedTenantAndRejectsExpiredOrRevokedSessions()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")!;
        var organization = new Organization("Authentication test organization", $"auth-{Guid.NewGuid():N}");
        var otherOrganization = new Organization("Other test organization", $"other-{Guid.NewGuid():N}");
        var user = new User(organization.Id, $"auth-{Guid.NewGuid():N}@example.test", "Auth test user",
            new Pbkdf2PasswordHasher().HashPassword("Local-test-password-42!"));
        var options = new DbContextOptionsBuilder<SmartShopPosDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using (var setupContext = new SmartShopPosDbContext(options))
        {
            await setupContext.Database.MigrateAsync();
            setupContext.AddRange(organization, otherOrganization, user);
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

            using var unauthenticatedResponse = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedResponse.StatusCode);

            using var loginResponse = await LoginAsync(client, user.Email, organization.Code, "Local-test-password-42!");
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            var originalCookies = GetCookieHeader(loginResponse);
            var sessionToken = GetCookieValue(originalCookies, "ssps_session");
            var decodedSessionToken = Uri.UnescapeDataString(sessionToken);
            var authenticatedUser = await loginResponse.Content.ReadFromJsonAsync<AuthenticatedUserResponse>();
            Assert.NotNull(authenticatedUser);
            Assert.Equal(organization.Id, authenticatedUser.OrganizationId);

            Guid expiredSessionId;
            await using (var verificationContext = new SmartShopPosDbContext(options))
            {
                var persistedSession = await verificationContext.AuthenticationSessions
                    .SingleAsync(session => session.UserId == user.Id);
                expiredSessionId = persistedSession.SessionId;
                Assert.Equal(organization.Id, persistedSession.OrganizationId);
                Assert.Equal(AuthenticationSecurity.HashToken(decodedSessionToken), persistedSession.TokenHash);
                Assert.NotEqual(sessionToken, persistedSession.TokenHash);
            }

            using (var meRequest = CreateMeRequest(originalCookies, otherOrganization.Id))
            using (var meResponse = await client.SendAsync(meRequest))
            {
                Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
                using var responseJson = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync());
                Assert.Equal(organization.Id, responseJson.RootElement.GetProperty("organizationId").GetGuid());
            }

            await using (var updateContext = new SmartShopPosDbContext(options))
            {
                await updateContext.AuthenticationSessions
                    .Where(session => session.SessionId == expiredSessionId)
                    .ExecuteUpdateAsync(update => update.SetProperty(
                        session => session.ExpiresAt,
                        DateTimeOffset.UtcNow.AddMinutes(-1)));
            }

            using (var expiredRequest = CreateMeRequest(originalCookies, otherOrganization.Id))
            using (var expiredResponse = await client.SendAsync(expiredRequest))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, expiredResponse.StatusCode);
            }

            using var secondLoginResponse = await LoginAsync(client, user.Email, organization.Code, "Local-test-password-42!");
            Assert.Equal(HttpStatusCode.OK, secondLoginResponse.StatusCode);
            var revocationCookies = GetCookieHeader(secondLoginResponse);
            Guid revokedSessionId;
            await using (var verificationContext = new SmartShopPosDbContext(options))
            {
                revokedSessionId = await verificationContext.AuthenticationSessions
                    .Where(session => session.UserId == user.Id && session.RevokedAt == null && session.ExpiresAt > DateTimeOffset.UtcNow)
                    .Select(session => session.SessionId)
                    .SingleAsync();
            }

            using (var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout"))
            {
                logoutRequest.Headers.Add("Cookie", revocationCookies);
                using var logoutResponse = await client.SendAsync(logoutRequest);
                Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
            }

            await using (var verificationContext = new SmartShopPosDbContext(options))
            {
                var revokedSession = await verificationContext.AuthenticationSessions
                    .SingleAsync(session => session.SessionId == revokedSessionId);
                Assert.NotNull(revokedSession.RevokedAt);
            }

            using (var revokedRequest = CreateMeRequest(revocationCookies, otherOrganization.Id))
            using (var revokedResponse = await client.SendAsync(revokedRequest))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, revokedResponse.StatusCode);
            }
        }
        finally
        {
            await using var cleanupContext = new SmartShopPosDbContext(options);
            var organizationIds = new[] { organization.Id, otherOrganization.Id };
            cleanupContext.AuthenticationSessions.RemoveRange(await cleanupContext.AuthenticationSessions
                .Where(session => organizationIds.Contains(session.OrganizationId)).ToListAsync());
            cleanupContext.Users.RemoveRange(await cleanupContext.Users
                .Where(existingUser => organizationIds.Contains(existingUser.OrganizationId)).ToListAsync());
            cleanupContext.Organizations.RemoveRange(await cleanupContext.Organizations
                .Where(existingOrganization => organizationIds.Contains(existingOrganization.Id)).ToListAsync());
            await cleanupContext.SaveChangesAsync();
        }
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string organizationCode, string password)
    {
        return client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, organizationCode, password));
    }

    private static HttpRequestMessage CreateMeRequest(string cookies, Guid suppliedOrganizationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/auth/me?organizationId={suppliedOrganizationId}");
        request.Headers.Add("Cookie", cookies);
        request.Headers.Add("X-Organization-Id", suppliedOrganizationId.ToString());
        return request;
    }

    private static string GetCookieHeader(HttpResponseMessage response)
    {
        return string.Join("; ", response.Headers.GetValues("Set-Cookie")
            .Select(cookie => cookie.Split(';', 2)[0]));
    }

    private static string GetCookieValue(string cookieHeader, string name)
    {
        var cookie = cookieHeader.Split(';', StringSplitOptions.TrimEntries)
            .Single(value => value.StartsWith($"{name}=", StringComparison.Ordinal));
        return cookie[(name.Length + 1)..];
    }
}
