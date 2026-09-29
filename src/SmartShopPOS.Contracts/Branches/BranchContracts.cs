namespace SmartShopPOS.Contracts.Branches;

public sealed record CreateBranchRequest(string Code, string Name);

public sealed record BranchResponse(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);