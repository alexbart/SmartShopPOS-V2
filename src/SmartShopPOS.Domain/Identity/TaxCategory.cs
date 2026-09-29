using System.Text.RegularExpressions;

namespace SmartShopPOS.Domain.Identity;

public sealed class TaxCategory : Entity
{
    private static readonly Regex ValidCode = new("^[A-Z0-9]+(?:[_-][A-Z0-9]+)*$", RegexOptions.Compiled);

    private TaxCategory()
    {
    }

    public TaxCategory(Guid organizationId, string code, string name, decimal rate)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A tax category must belong to an organization.");
        }

        OrganizationId = organizationId;
        SetDetails(code, name, rate);
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid OrganizationId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public decimal Rate { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Organization Organization { get; private set; } = null!;

    public void Update(string code, string name, decimal rate)
    {
        SetDetails(code, name, rate);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void SetDetails(string code, string name, decimal rate)
    {
        Code = Organization.Required(code, nameof(code), 32).ToUpperInvariant();
        if (!ValidCode.IsMatch(Code))
        {
            throw new DomainException("Tax category code must contain letters, numbers, underscores, and hyphens only.");
        }

        Name = Organization.Required(name, nameof(name), 120);
        if (rate is < 0 or > 100)
        {
            throw new DomainException("Tax category rate must be between 0 and 100 percent.");
        }

        Rate = rate;
    }
}