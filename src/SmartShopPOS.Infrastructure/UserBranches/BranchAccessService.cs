using Microsoft.EntityFrameworkCore;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Application.UserBranches;
using SmartShopPOS.Contracts.UserBranches;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.Infrastructure.UserBranches;

public sealed class BranchAccessService(
    SmartShopPosDbContext dbContext,
    ICurrentUser currentUser,
    IPermissionChecker permissionChecker) : IBranchAccessService
{
    public async Task<UserBranchResult<IReadOnlyList<AccessibleBranchResponse>>> GetAccessibleBranchesAsync(
        CancellationToken cancellationToken = default)
    {
        var session = await GetCurrentSessionAsync(cancellationToken);
        if (session is null)
        {
            return UserBranchResult<IReadOnlyList<AccessibleBranchResponse>>.Failure(
                UserBranchError.Unauthenticated, "Authentication is required.");
        }

        if (!await HasSelectPermissionAsync(session.UserId, cancellationToken))
        {
            return UserBranchResult<IReadOnlyList<AccessibleBranchResponse>>.Failure(
                UserBranchError.Forbidden, "The branch context permission is missing.");
        }

        var branches = await dbContext.UserBranches
            .Where(assignment => assignment.OrganizationId == session.OrganizationId &&
                assignment.UserId == session.UserId && assignment.DeactivatedAt == null)
            .Join(dbContext.Branches,
                assignment => new { assignment.OrganizationId, assignment.BranchId },
                branch => new { branch.OrganizationId, BranchId = branch.Id },
                (assignment, branch) => branch)
            .Where(branch => branch.IsActive)
            .OrderBy(branch => branch.Code)
            .Select(branch => new AccessibleBranchResponse(branch.Id, branch.Code, branch.Name))
            .ToListAsync(cancellationToken);

        return UserBranchResult<IReadOnlyList<AccessibleBranchResponse>>.Success(branches);
    }

    public async Task<UserBranchResult<BranchContextResponse?>> GetCurrentContextAsync(
        CancellationToken cancellationToken = default)
    {
        var session = await GetCurrentSessionAsync(cancellationToken);
        if (session is null)
        {
            return UserBranchResult<BranchContextResponse?>.Failure(
                UserBranchError.Unauthenticated, "Authentication is required.");
        }

        if (!await HasSelectPermissionAsync(session.UserId, cancellationToken))
        {
            return UserBranchResult<BranchContextResponse?>.Failure(
                UserBranchError.Forbidden, "The branch context permission is missing.");
        }

        if (session.SelectedBranchId is not Guid branchId)
        {
            return UserBranchResult<BranchContextResponse?>.Success(null);
        }

        var branch = await GetAccessibleBranchAsync(session, branchId, cancellationToken);
        if (branch is null)
        {
            session.SetSelectedBranch(null);
            await dbContext.SaveChangesAsync(cancellationToken);
            return UserBranchResult<BranchContextResponse?>.Success(null);
        }

        return UserBranchResult<BranchContextResponse?>.Success(
            new BranchContextResponse(branch.Id, branch.Code, branch.Name));
    }

    public async Task<UserBranchResult<BranchContextResponse>> SelectContextAsync(
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetCurrentSessionAsync(cancellationToken);
        if (session is null)
        {
            return UserBranchResult<BranchContextResponse>.Failure(
                UserBranchError.Unauthenticated, "Authentication is required.");
        }

        if (!await HasSelectPermissionAsync(session.UserId, cancellationToken))
        {
            return UserBranchResult<BranchContextResponse>.Failure(
                UserBranchError.Forbidden, "The branch context permission is missing.");
        }

        if (branchId == Guid.Empty)
        {
            return UserBranchResult<BranchContextResponse>.Failure(UserBranchError.Invalid, "A branch identifier is required.");
        }

        var branch = await dbContext.Branches.SingleOrDefaultAsync(
            candidate => candidate.Id == branchId && candidate.OrganizationId == session.OrganizationId,
            cancellationToken);
        if (branch is null)
        {
            return UserBranchResult<BranchContextResponse>.Failure(UserBranchError.NotFound, "Branch not found.");
        }

        if (!branch.IsActive)
        {
            return UserBranchResult<BranchContextResponse>.Failure(UserBranchError.Conflict, "An inactive branch cannot be selected.");
        }

        var assigned = await dbContext.UserBranches.AnyAsync(
            assignment => assignment.OrganizationId == session.OrganizationId && assignment.UserId == session.UserId &&
                assignment.BranchId == branchId && assignment.DeactivatedAt == null,
            cancellationToken);
        if (!assigned)
        {
            return UserBranchResult<BranchContextResponse>.Failure(UserBranchError.Forbidden, "The user is not assigned to this branch.");
        }

        session.SetSelectedBranch(branchId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return UserBranchResult<BranchContextResponse>.Success(
            new BranchContextResponse(branch.Id, branch.Code, branch.Name));
    }

    public async Task<bool> CanOperateInBranchAsync(
        Guid branchId,
        string requiredPermission,
        CancellationToken cancellationToken = default)
    {
        var session = await GetCurrentSessionAsync(cancellationToken);
        if (session is null || session.SelectedBranchId != branchId || string.IsNullOrWhiteSpace(requiredPermission))
        {
            return false;
        }

        if (!await HasSelectPermissionAsync(session.UserId, cancellationToken) ||
            !await permissionChecker.HasPermissionAsync(session.UserId, requiredPermission, cancellationToken))
        {
            return false;
        }

        return await GetAccessibleBranchAsync(session, branchId, cancellationToken) is not null;
    }

    private async Task<Domain.Identity.AuthenticationSession?> GetCurrentSessionAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty ||
            currentUser.OrganizationId is not Guid organizationId || organizationId == Guid.Empty ||
            currentUser.SessionId is not Guid sessionId || sessionId == Guid.Empty)
        {
            return null;
        }

        var session = await dbContext.AuthenticationSessions.SingleOrDefaultAsync(
            candidate => candidate.SessionId == sessionId && candidate.UserId == userId &&
                candidate.OrganizationId == organizationId && candidate.RevokedAt == null &&
                candidate.ExpiresAt > DateTimeOffset.UtcNow,
            cancellationToken);
        if (session is null)
        {
            return null;
        }

        var activeIdentity = await dbContext.Users.AnyAsync(
            user => user.Id == userId && user.OrganizationId == organizationId && user.IsActive && user.Organization.IsActive,
            cancellationToken);
        return activeIdentity ? session : null;
    }

    private Task<bool> HasSelectPermissionAsync(Guid userId, CancellationToken cancellationToken)
    {
        return permissionChecker.HasPermissionAsync(userId, "branch_context.select", cancellationToken);
    }

    private Task<Domain.Identity.Branch?> GetAccessibleBranchAsync(
        Domain.Identity.AuthenticationSession session,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        return dbContext.Branches.SingleOrDefaultAsync(branch =>
            branch.Id == branchId && branch.OrganizationId == session.OrganizationId && branch.IsActive &&
            dbContext.UserBranches.Any(assignment => assignment.OrganizationId == session.OrganizationId &&
                assignment.UserId == session.UserId && assignment.BranchId == branchId && assignment.DeactivatedAt == null),
            cancellationToken);
    }
}