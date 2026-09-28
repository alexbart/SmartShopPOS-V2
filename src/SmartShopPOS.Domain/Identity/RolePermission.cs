namespace SmartShopPOS.Domain.Identity;

public sealed class RolePermission
{
    private RolePermission()
    {
    }

    public RolePermission(Guid roleId, Guid permissionId)
    {
        if (roleId == Guid.Empty || permissionId == Guid.Empty)
        {
            throw new DomainException("Role permission assignments require role and permission identifiers.");
        }

        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public Role Role { get; private set; } = null!;
    public Permission Permission { get; private set; } = null!;
}