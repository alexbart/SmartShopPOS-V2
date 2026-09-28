using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Application.Identity;

public interface IIdentityWriter
{
    Task AddOrganizationAsync(Organization organization, CancellationToken cancellationToken = default);

    Task AddUserAsync(User user, CancellationToken cancellationToken = default);

    Task AddRoleAsync(Role role, CancellationToken cancellationToken = default);

    Task AddPermissionAsync(Permission permission, CancellationToken cancellationToken = default);

    Task AssignRoleAsync(UserRole assignment, CancellationToken cancellationToken = default);

    Task AssignPermissionAsync(RolePermission assignment, CancellationToken cancellationToken = default);
}