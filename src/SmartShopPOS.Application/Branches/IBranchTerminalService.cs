using SmartShopPOS.Contracts.Branches;

namespace SmartShopPOS.Application.Branches;

public interface IBranchTerminalService
{
    Task<BranchTerminalResult<IReadOnlyList<BranchResponse>>> GetBranchesAsync(CancellationToken cancellationToken = default);

    Task<BranchTerminalResult<BranchResponse>> CreateBranchAsync(
        CreateBranchRequest request,
        CancellationToken cancellationToken = default);

    Task<BranchTerminalResult<IReadOnlyList<TerminalResponse>>> GetTerminalsAsync(
        Guid branchId,
        CancellationToken cancellationToken = default);

    Task<BranchTerminalResult<TerminalResponse>> CreateTerminalAsync(
        Guid branchId,
        CreateTerminalRequest request,
        CancellationToken cancellationToken = default);
}