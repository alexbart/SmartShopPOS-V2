using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Application.UserBranches;
using SmartShopPOS.Contracts.UserBranches;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.Infrastructure.UserBranches;

public sealed class UserBranchAssignmentService(
    SmartShopPosDbContext dbContext,
    ICurrentUser currentUser,
    IPermissionChecker permissionChecker) : IUserBranchAssignmentService
{
    public async Task<UserBranchResult<IReadOnlyList<UserBranchAssignmentResponse>>> GetAssignmentsAsync(
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var error = await AuthorizeAsync("user_branch_assignments.view", cancellationToken);
        if (error is not null)
        {
            return UserBranchResult<IReadOnlyList<UserBranchAssignmentResponse>>.Failure(error.Value, Message(error.Value));
        }

        var organizationId = currentUser.OrganizationId!.Value;
        var branchExists = await dbContext.Branches.AnyAsync(
            branch => branch.Id == branchId && branch.OrganizationId == organizationId,
            cancellationToken);
        if (!branchExists)
        {
            return UserBranchResult<IReadOnlyList<UserBranchAssignmentResponse>>.Failure(UserBranchError.NotFound, "Branch not found.");
        }

        var assignments = await dbContext.UserBranches
            .Where(assignment => assignment.OrganizationId == organizationId && assignment.BranchId == branchId)
            .Join(dbContext.Users,
                assignment => new { assignment.OrganizationId, assignment.UserId },
                user => new { user.OrganizationId, UserId = user.Id },
                (assignment, user) => new { assignment, user })
            .OrderBy(item => item.user.DisplayName)
            .Select(item => new UserBranchAssignmentResponse(
                    item.assignment.Id,
                    item.user.Id,
                    item.user.Email,
                    item.user.DisplayName,
                    item.assignment.BranchId,
                    item.assignment.DeactivatedAt == null,
                    item.assignment.CreatedAt,
                    item.assignment.DeactivatedAt))
            .ToListAsync(cancellationToken);

        return UserBranchResult<IReadOnlyList<UserBranchAssignmentResponse>>.Success(assignments);
    }

    public async Task<UserBranchResult<UserBranchAssignmentResponse>> AssignAsync(
        Guid branchId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var error = await AuthorizeAsync("user_branch_assignments.create", cancellationToken);
        if (error is not null)
        {
            return UserBranchResult<UserBranchAssignmentResponse>.Failure(error.Value, Message(error.Value));
        }

        var organizationId = currentUser.OrganizationId!.Value;
        if (userId == Guid.Empty)
        {
            return UserBranchResult<UserBranchAssignmentResponse>.Failure(UserBranchError.Invalid, "A user identifier is required.");
        }

        var branch = await dbContext.Branches.SingleOrDefaultAsync(
            candidate => candidate.Id == branchId && candidate.OrganizationId == organizationId,
            cancellationToken);
        if (branch is null)
        {
            return UserBranchResult<UserBranchAssignmentResponse>.Failure(UserBranchError.NotFound, "Branch not found.");
        }

        if (!branch.IsActive)
        {
            return UserBranchResult<UserBranchAssignmentResponse>.Failure(UserBranchError.Conflict, "An inactive branch cannot receive assignments.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == userId && candidate.OrganizationId == organizationId,
            cancellationToken);
        if (user is null)
        {
            return UserBranchResult<UserBranchAssignmentResponse>.Failure(UserBranchError.NotFound, "User not found.");
        }

        if (!user.IsActive)
        {
            return UserBranchResult<UserBranchAssignmentResponse>.Failure(UserBranchError.Conflict, "An inactive user cannot receive assignments.");
        }

        var alreadyAssigned = await dbContext.UserBranches.AnyAsync(
            assignment => assignment.OrganizationId == organizationId && assignment.UserId == userId &&
                assignment.BranchId == branchId && assignment.DeactivatedAt == null,
            cancellationToken);
        if (alreadyAssigned)
        {
            return UserBranchResult<UserBranchAssignmentResponse>.Failure(UserBranchError.Conflict, "The user is already assigned to this branch.");
        }

        var assignment = new UserBranch(organizationId, userId, branchId);
        dbContext.UserBranches.Add(assignment);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return UserBranchResult<UserBranchAssignmentResponse>.Failure(UserBranchError.Conflict, "The user is already assigned to this branch.");
        }

        return UserBranchResult<UserBranchAssignmentResponse>.Success(ToResponse(assignment, user));
    }

    public async Task<UserBranchResult<UserBranchAssignmentResponse>> DeactivateAsync(
        Guid branchId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var error = await AuthorizeAsync("user_branch_assignments.deactivate", cancellationToken);
        if (error is not null)
        {
            return UserBranchResult<UserBranchAssignmentResponse>.Failure(error.Value, Message(error.Value));
        }

        var organizationId = currentUser.OrganizationId!.Value;
        var assignment = await dbContext.UserBranches.SingleOrDefaultAsync(
            candidate => candidate.OrganizationId == organizationId && candidate.BranchId == branchId &&
                candidate.UserId == userId && candidate.DeactivatedAt == null,
            cancellationToken);
        if (assignment is null)
        {
            return UserBranchResult<UserBranchAssignmentResponse>.Failure(UserBranchError.NotFound, "Active assignment not found.");
        }

        var user = await dbContext.Users.SingleAsync(
            candidate => candidate.Id == userId && candidate.OrganizationId == organizationId,
            cancellationToken);
        assignment.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        return UserBranchResult<UserBranchAssignmentResponse>.Success(ToResponse(assignment, user));
    }

    private async Task<UserBranchError?> AuthorizeAsync(string permission, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty ||
            currentUser.OrganizationId is not Guid organizationId || organizationId == Guid.Empty)
        {
            return UserBranchError.Unauthenticated;
        }

        return await permissionChecker.HasPermissionAsync(userId, permission, cancellationToken)
            ? null
            : UserBranchError.Forbidden;
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }

    private static string Message(UserBranchError error)
    {
        return error == UserBranchError.Unauthenticated
            ? "Authentication is required."
            : "The required permission is missing.";
    }

    private static UserBranchAssignmentResponse ToResponse(UserBranch assignment, User user)
    {
        return new UserBranchAssignmentResponse(
            assignment.Id,
            user.Id,
            user.Email,
            user.DisplayName,
            assignment.BranchId,
            assignment.IsActive,
            assignment.CreatedAt,
            assignment.DeactivatedAt);
    }
}