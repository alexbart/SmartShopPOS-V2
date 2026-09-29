namespace SmartShopPOS.Contracts.Branches;

public sealed record CreateTerminalRequest(string Code, string Name);

public sealed record TerminalResponse(
    Guid Id,
    Guid BranchId,
    string Code,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastSeenAt);