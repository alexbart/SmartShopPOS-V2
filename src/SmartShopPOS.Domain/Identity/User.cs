namespace SmartShopPOS.Domain.Identity;

public sealed class User : Entity
{
    private User()
    {
    }

    public User(Guid organizationId, string email, string displayName, string passwordHash)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A user must belong to an organization.");
        }

        OrganizationId = organizationId;
        Email = Organization.Required(email, nameof(email), 320);
        if (!Email.Contains('@', StringComparison.Ordinal) || Email.StartsWith('@') || Email.EndsWith('@'))
        {
            throw new DomainException("A valid email address is required.");
        }

        NormalizedEmail = Email.ToUpperInvariant();
        DisplayName = Organization.Required(displayName, nameof(displayName), 120);
        PasswordHash = Organization.Required(passwordHash, nameof(passwordHash), 512);
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid OrganizationId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    public Organization Organization { get; private set; } = null!;

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}