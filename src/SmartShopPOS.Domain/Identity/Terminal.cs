using System.Text.RegularExpressions;

namespace SmartShopPOS.Domain.Identity;

public sealed class Terminal : Entity
{
    private static readonly Regex ValidCode = new("^[A-Z0-9]+(?:-[A-Z0-9]+)*$", RegexOptions.Compiled);

    private Terminal()
    {
    }

    public Terminal(Guid organizationId, Guid branchId, string code, string name)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
        {
            throw new DomainException("A terminal must belong to an organization and branch.");
        }

        OrganizationId = organizationId;
        BranchId = branchId;
        Code = NormalizeCode(code);
        Name = Organization.Required(name, nameof(name), 120);
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid OrganizationId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? LastSeenAt { get; private set; }

    public Branch Branch { get; private set; } = null!;

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkSeen(DateTimeOffset utcNow)
    {
        LastSeenAt = utcNow.ToUniversalTime();
    }

    private static string NormalizeCode(string code)
    {
        var normalized = Organization.Required(code, nameof(code), 32).ToUpperInvariant();
        if (!ValidCode.IsMatch(normalized))
        {
            throw new DomainException("Terminal code must contain letters, numbers, and single hyphens only.");
        }

        return normalized;
    }
}