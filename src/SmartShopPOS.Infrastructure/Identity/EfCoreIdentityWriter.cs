using SmartShopPOS.Application.Identity;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.Infrastructure.Identity;

public sealed class EfCoreIdentityWriter(SmartShopPosDbContext dbContext) : IIdentityWriter
{
    public async Task AddOrganizationAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        await dbContext.Organizations.AddAsync(organization, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddUserAsync(User user, CancellationToken cancellationToken = default)
    {
        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRoleAsync(Role role, CancellationToken cancellationToken = default)
    {
        await dbContext.Roles.AddAsync(role, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddPermissionAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        await dbContext.Permissions.AddAsync(permission, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignRoleAsync(UserRole assignment, CancellationToken cancellationToken = default)
    {
        await dbContext.UserRoles.AddAsync(assignment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignPermissionAsync(RolePermission assignment, CancellationToken cancellationToken = default)
    {
        await dbContext.RolePermissions.AddAsync(assignment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}