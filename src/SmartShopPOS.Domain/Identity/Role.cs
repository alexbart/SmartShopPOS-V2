namespace SmartShopPOS.Domain.Identity;

public sealed class Role : Entity
{
    private Role()
    {
    }

    public Role(Guid organizationId, string name, string? description = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A role must belong to an organization.");
        }

        OrganizationId = organizationId;
        Name = Organization.Required(name, nameof(name), 100);
        NormalizedName = Name.ToUpperInvariant();
        Description = description?.Trim();
        if (Description?.Length > 500)
        {
            throw new DomainException("Role description must not exceed 500 characters.");
        }

        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Organization Organization { get; private set; } = null!;
    public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}