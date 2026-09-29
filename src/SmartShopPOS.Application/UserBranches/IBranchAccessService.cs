using SmartShopPOS.Contracts.UserBranches;

namespace SmartShopPOS.Application.UserBranches;

public interface IBranchAccessService
{
    Task<UserBranchResult<IReadOnlyList<AccessibleBranchResponse>>> GetAccessibleBranchesAsync(
        CancellationToken cancellationToken = default);

    Task<UserBranchResult<BranchContextResponse?>> GetCurrentContextAsync(
        CancellationToken cancellationToken = default);

    Task<UserBranchResult<BranchContextResponse>> SelectContextAsync(
        Guid branchId,
        CancellationToken cancellationToken = default);

    Task<bool> CanOperateInBranchAsync(
        Guid branchId,
        string requiredPermission,
        CancellationToken cancellationToken = default);
}