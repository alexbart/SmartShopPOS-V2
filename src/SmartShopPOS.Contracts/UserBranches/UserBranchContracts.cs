namespace SmartShopPOS.Contracts.UserBranches;

public sealed record AssignUserToBranchRequest(Guid UserId);

public sealed record SelectBranchContextRequest(Guid BranchId);

public sealed record UserBranchAssignmentResponse(
    Guid Id,
    Guid UserId,
    string Email,
    string DisplayName,
    Guid BranchId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeactivatedAt);

public sealed record AccessibleBranchResponse(Guid Id, string Code, string Name);

public sealed record BranchContextResponse(Guid BranchId, string Code, string Name);