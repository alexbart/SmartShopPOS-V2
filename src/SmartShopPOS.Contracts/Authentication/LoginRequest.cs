namespace SmartShopPOS.Contracts.Authentication;

public sealed record LoginRequest(
    string Email,
    string OrganizationCode,
    string Password);

public sealed record AuthenticatedUserResponse(
    Guid Id,
    string Email,
    Guid OrganizationId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
