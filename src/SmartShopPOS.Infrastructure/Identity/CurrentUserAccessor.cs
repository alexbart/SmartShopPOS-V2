using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.Infrastructure.Identity;

public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor, SmartShopPosDbContext dbContext) : ICurrentUser
{
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User?.FindFirst("user_id")?.Value
                ?? httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public Guid? OrganizationId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User?.FindFirst("organization_id")?.Value
                ?? httpContextAccessor.HttpContext?.User?.FindFirst("tenant_id")?.Value;

            return Guid.TryParse(value, out var organizationId) ? organizationId : null;
        }
    }

    public Guid? SessionId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User?.FindFirst("session_id")?.Value;
            return Guid.TryParse(value, out var sessionId) ? sessionId : null;
        }
    }

    public Guid? SelectedBranchId
    {
        get
        {
            if (SessionId is not Guid sessionId)
            {
                return null;
            }

            // Query the session from DB to get the selected branch
            var session = dbContext.AuthenticationSessions
                .AsNoTracking()
                .SingleOrDefault(s => s.SessionId == sessionId && s.RevokedAt == null && s.ExpiresAt > DateTimeOffset.UtcNow);

            return session?.SelectedBranchId;
        }
    }

    public string? Email => httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;
}
