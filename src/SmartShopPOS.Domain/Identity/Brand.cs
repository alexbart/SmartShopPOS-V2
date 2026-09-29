namespace SmartShopPOS.Domain.Identity;

public sealed class Brand : Entity
{
    private Brand()
    {
    }

    public Brand(Guid organizationId, string name, string? description = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A brand must belong to an organization.");
        }

        OrganizationId = organizationId;
        SetDetails(name, description);
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

    public void Update(string name, string? description)
    {
        SetDetails(name, description);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void SetDetails(string name, string? description)
    {
        Name = Organization.Required(name, nameof(name), 120);
        NormalizedName = Name.ToUpperInvariant();
        Description = CatalogValidation.OptionalDescription(description);
    }
}