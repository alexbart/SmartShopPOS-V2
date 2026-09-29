using System.Net.Mail;

namespace SmartShopPOS.Domain.Identity;

public sealed class Supplier : Entity
{
    private Supplier()
    {
    }

    public Supplier(
        Guid organizationId,
        string code,
        string name,
        string? description = null,
        string? contactPerson = null,
        string? phone = null,
        string? email = null,
        string? address = null,
        string? taxIdentifier = null,
        string? businessRegistrationNumber = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A supplier must belong to an organization.");
        }

        OrganizationId = organizationId;
        SetDetails(code, name, description, contactPerson, phone, email, address, taxIdentifier, businessRegistrationNumber);
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid OrganizationId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ContactPerson { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string? TaxIdentifier { get; private set; }
    public string? BusinessRegistrationNumber { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Organization Organization { get; private set; } = null!;

    public void Update(
        string code,
        string name,
        string? description,
        string? contactPerson,
        string? phone,
        string? email,
        string? address,
        string? taxIdentifier,
        string? businessRegistrationNumber)
    {
        SetDetails(code, name, description, contactPerson, phone, email, address, taxIdentifier, businessRegistrationNumber);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void SetDetails(
        string code,
        string name,
        string? description,
        string? contactPerson,
        string? phone,
        string? email,
        string? address,
        string? taxIdentifier,
        string? businessRegistrationNumber)
    {
        Code = Organization.Required(code, nameof(code), 64).ToUpperInvariant();
        Name = Organization.Required(name, nameof(name), 200);
        Description = CatalogValidation.OptionalValue(description, nameof(description), 1000);
        ContactPerson = CatalogValidation.OptionalValue(contactPerson, nameof(contactPerson), 160);
        Phone = CatalogValidation.OptionalValue(phone, nameof(phone), 64);
        Email = NormalizeEmail(email);
        Address = CatalogValidation.OptionalValue(address, nameof(address), 1000);
        TaxIdentifier = CatalogValidation.OptionalValue(taxIdentifier, nameof(taxIdentifier), 64);
        BusinessRegistrationNumber = CatalogValidation.OptionalValue(businessRegistrationNumber, nameof(businessRegistrationNumber), 64);
    }

    private static string? NormalizeEmail(string? email)
    {
        var value = CatalogValidation.OptionalValue(email, nameof(email), 320);
        if (value is null)
        {
            return null;
        }

        if (!MailAddress.TryCreate(value, out var address) || !string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Email must be a valid email address.");
        }

        return value;
    }
}
