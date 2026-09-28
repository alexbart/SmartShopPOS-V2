using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Contracts.Authentication;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.Infrastructure.Identity;

public sealed class AuthenticationService(
    SmartShopPosDbContext dbContext,
    IPasswordHasher passwordHasher,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : SmartShopPOS.Application.Identity.IAuthenticationService
{
    public async Task<AuthenticatedUserResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var email = request.Email?.Trim();
        var organizationCode = request.OrganizationCode?.Trim();
        var password = request.Password;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(organizationCode) || string.IsNullOrWhiteSpace(password))
        {
            throw new UnauthorizedAccessException("Authentication failed.");
        }

        var normalizedEmail = email.ToUpperInvariant();
        var normalizedOrganizationCode = organizationCode.ToLowerInvariant();

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(organization => organization.Code == normalizedOrganizationCode && organization.IsActive, cancellationToken);

        if (organization is null)
        {
            throw new UnauthorizedAccessException("Authentication failed.");
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(user => user.OrganizationId == organization.Id && user.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Authentication failed.");
        }

        if (!passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Authentication failed.");
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var lifetimeMinutes = configuration.GetValue<int?>("Authentication:SessionLifetimeMinutes") ?? 60;
        var session = new AuthenticationSession(
            userId: user.Id,
            organizationId: organization.Id,
            sessionId: Guid.NewGuid(),
            tokenHash: AuthenticationSecurity.HashToken(token),
            createdAt: DateTimeOffset.UtcNow,
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(lifetimeMinutes));

        await dbContext.AuthenticationSessions.AddAsync(session, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            var principal = CreatePrincipal(user, organization, session);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = session.ExpiresAt,
                AllowRefresh = false
            };

            httpContext.Response.Cookies.Append(
                "ssps_session",
                token,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = session.ExpiresAt.UtcDateTime,
                    IsEssential = true,
                    Path = "/"
                });

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties);
        }

        var roles = await GetUserRoleNamesAsync(user.Id, organization.Id, cancellationToken);
        var permissions = await GetUserPermissionKeysAsync(user.Id, organization.Id, cancellationToken);

        return new AuthenticatedUserResponse(user.Id, user.Email, user.OrganizationId, roles, permissions);
    }

    public async Task<AuthenticatedUserResponse?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null || !httpContext.User.Identity?.IsAuthenticated == true)
        {
            return null;
        }

        var userId = GetGuidClaim(httpContext.User, "user_id") ?? GetGuidClaim(httpContext.User, ClaimTypes.NameIdentifier);
        var organizationId = GetGuidClaim(httpContext.User, "organization_id") ?? GetGuidClaim(httpContext.User, "tenant_id");

        if (userId is null || organizationId is null)
        {
            return null;
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(user => user.Id == userId.Value && user.OrganizationId == organizationId.Value && user.IsActive, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roles = await GetUserRoleNamesAsync(user.Id, user.OrganizationId, cancellationToken);
        var permissions = await GetUserPermissionKeysAsync(user.Id, user.OrganizationId, cancellationToken);

        return new AuthenticatedUserResponse(user.Id, user.Email, user.OrganizationId, roles, permissions);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        var sessionId = GetGuidClaim(httpContext.User, "session_id");
        if (sessionId is not null)
        {
            var session = await dbContext.AuthenticationSessions
                .SingleOrDefaultAsync(session => session.SessionId == sessionId.Value, cancellationToken);

            if (session is not null)
            {
                session.Revoke();
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public async Task<bool> ValidateSessionTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return false;
        }

        var tokenHash = AuthenticationSecurity.HashToken(rawToken);
        var session = await dbContext.AuthenticationSessions
            .SingleOrDefaultAsync(session => session.TokenHash == tokenHash, cancellationToken);

        if (session is null)
        {
            return false;
        }

        if (session.IsExpired(DateTimeOffset.UtcNow))
        {
            return false;
        }

        session.Touch(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static Guid? GetGuidClaim(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    private static ClaimsPrincipal CreatePrincipal(User user, Organization organization, AuthenticationSession session)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("user_id", user.Id.ToString()),
            new Claim("organization_id", organization.Id.ToString()),
            new Claim("tenant_id", organization.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("session_id", session.SessionId.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    private async Task<List<string>> GetUserRoleNamesAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        return await dbContext.UserRoles
            .Where(roleAssignment => roleAssignment.UserId == userId && roleAssignment.OrganizationId == organizationId)
            .Select(roleAssignment => roleAssignment.Role.Name)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<string>> GetUserPermissionKeysAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        return await dbContext.UserRoles
            .Where(roleAssignment => roleAssignment.UserId == userId && roleAssignment.OrganizationId == organizationId)
            .SelectMany(roleAssignment => roleAssignment.Role.RolePermissions)
            .Select(rolePermission => rolePermission.Permission.Key)
            .Distinct()
            .OrderBy(permissionKey => permissionKey)
            .ToListAsync(cancellationToken);
    }
}
