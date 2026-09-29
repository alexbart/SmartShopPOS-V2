using System.Text.RegularExpressions;

namespace SmartShopPOS.Domain.Identity;

public sealed class Branch : Entity
{
    private static readonly Regex ValidCode = new("^[A-Z0-9]+(?:-[A-Z0-9]+)*$", RegexOptions.Compiled);

    private Branch()
    {
    }

    public Branch(Guid organizationId, string code, string name)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A branch must belong to an organization.");
        }

        OrganizationId = organizationId;
        Code = NormalizeCode(code);
        Name = Organization.Required(name, nameof(name), 160);
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid OrganizationId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Organization Organization { get; private set; } = null!;
    public ICollection<Terminal> Terminals { get; private set; } = new List<Terminal>();

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string NormalizeCode(string code)
    {
        var normalized = Organization.Required(code, nameof(code), 32).ToUpperInvariant();
        if (!ValidCode.IsMatch(normalized))
        {
            throw new DomainException("Branch code must contain letters, numbers, and single hyphens only.");
        }

        return normalized;
    }
}