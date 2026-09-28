using Microsoft.EntityFrameworkCore;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.Infrastructure.Identity;

public sealed class EfCorePermissionChecker(SmartShopPosDbContext dbContext) : IPermissionChecker
{
    public Task<bool> HasPermissionAsync(
        Guid userId,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedPermissionKey = permissionKey.Trim().ToLowerInvariant();

        return dbContext.UserRoles.AnyAsync(userRole =>
            userRole.UserId == userId &&
            userRole.User.IsActive &&
            userRole.User.Organization.IsActive &&
            userRole.Role.IsActive &&
            userRole.Role.OrganizationId == userRole.OrganizationId &&
            userRole.Role.RolePermissions.Any(rolePermission => rolePermission.Permission.Key == normalizedPermissionKey),
            cancellationToken);
    }
}