namespace SmartShopPOS.Domain.Identity;

public sealed class UserRole
{
    private UserRole()
    {
    }

    public UserRole(Guid organizationId, Guid userId, Guid roleId)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty || roleId == Guid.Empty)
        {
            throw new DomainException("User role assignments require organization, user, and role identifiers.");
        }

        OrganizationId = organizationId;
        UserId = userId;
        RoleId = roleId;
    }

    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public User User { get; private set; } = null!;
    public Role Role { get; private set; } = null!;
}