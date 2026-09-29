namespace SmartShopPOS.Contracts.Suppliers;

public sealed record SupplierUpsertRequest(
    string Code,
    string Name,
    string? Description,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? Address,
    string? TaxIdentifier,
    string? BusinessRegistrationNumber);

public sealed record SupplierResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? Address,
    string? TaxIdentifier,
    string? BusinessRegistrationNumber,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
public sealed record SupplierFilter(bool? Active = null);
