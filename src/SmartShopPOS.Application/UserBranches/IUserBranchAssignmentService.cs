using SmartShopPOS.Contracts.UserBranches;

namespace SmartShopPOS.Application.UserBranches;

public interface IUserBranchAssignmentService
{
    Task<UserBranchResult<IReadOnlyList<UserBranchAssignmentResponse>>> GetAssignmentsAsync(
        Guid branchId,
        CancellationToken cancellationToken = default);

    Task<UserBranchResult<UserBranchAssignmentResponse>> AssignAsync(
        Guid branchId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserBranchResult<UserBranchAssignmentResponse>> DeactivateAsync(
        Guid branchId,
        Guid userId,
        CancellationToken cancellationToken = default);
}