using System.Text.RegularExpressions;

namespace SmartShopPOS.Domain.Identity;

public sealed class UnitOfMeasure : Entity
{
    private static readonly Regex ValidCode = new("^[A-Z0-9]+(?:[_-][A-Z0-9]+)*$", RegexOptions.Compiled);

    private UnitOfMeasure()
    {
    }

    public UnitOfMeasure(Guid organizationId, string code, string name, string? symbol = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A unit of measure must belong to an organization.");
        }

        OrganizationId = organizationId;
        SetDetails(code, name, symbol);
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid OrganizationId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Symbol { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Organization Organization { get; private set; } = null!;

    public void Update(string code, string name, string? symbol)
    {
        SetDetails(code, name, symbol);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void SetDetails(string code, string name, string? symbol)
    {
        Code = Organization.Required(code, nameof(code), 32).ToUpperInvariant();
        if (!ValidCode.IsMatch(Code))
        {
            throw new DomainException("Unit code must contain letters, numbers, underscores, and hyphens only.");
        }

        Name = Organization.Required(name, nameof(name), 120);
        Symbol = CatalogValidation.OptionalValue(symbol, nameof(symbol), 16);
    }
}