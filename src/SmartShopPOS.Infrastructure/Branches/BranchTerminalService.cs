using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartShopPOS.Application.Branches;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Contracts.Branches;
using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.Infrastructure.Branches;

public sealed class BranchTerminalService(
    SmartShopPosDbContext dbContext,
    ICurrentUser currentUser,
    IPermissionChecker permissionChecker) : IBranchTerminalService
{
    public async Task<BranchTerminalResult<IReadOnlyList<BranchResponse>>> GetBranchesAsync(
        CancellationToken cancellationToken = default)
    {
        var authorizationError = await AuthorizeAsync("branches.view", cancellationToken);
        if (authorizationError is not null)
        {
            return BranchTerminalResult<IReadOnlyList<BranchResponse>>.Failure(authorizationError.Value, AuthorizationMessage(authorizationError.Value));
        }

        var branches = await dbContext.Branches
            .Where(branch => branch.OrganizationId == currentUser.OrganizationId)
            .OrderBy(branch => branch.Code)
            .ToListAsync(cancellationToken);

        return BranchTerminalResult<IReadOnlyList<BranchResponse>>.Success(branches.Select(ToResponse).ToList());
    }

    public async Task<BranchTerminalResult<BranchResponse>> CreateBranchAsync(
        CreateBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorizationError = await AuthorizeAsync("branches.create", cancellationToken);
        if (authorizationError is not null)
        {
            return BranchTerminalResult<BranchResponse>.Failure(authorizationError.Value, AuthorizationMessage(authorizationError.Value));
        }

        if (request is null)
        {
            return BranchTerminalResult<BranchResponse>.Failure(BranchTerminalError.Invalid, "A branch request is required.");
        }

        Branch branch;
        try
        {
            branch = new Branch(currentUser.OrganizationId!.Value, request.Code, request.Name);
        }
        catch (DomainException exception)
        {
            return BranchTerminalResult<BranchResponse>.Failure(BranchTerminalError.Invalid, exception.Message);
        }

        if (await dbContext.Branches.AnyAsync(existing =>
                existing.OrganizationId == branch.OrganizationId && existing.Code == branch.Code,
                cancellationToken))
        {
            return BranchTerminalResult<BranchResponse>.Failure(BranchTerminalError.Conflict, "A branch with this code already exists.");
        }

        dbContext.Branches.Add(branch);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return BranchTerminalResult<BranchResponse>.Failure(BranchTerminalError.Conflict, "A branch with this code already exists.");
        }

        return BranchTerminalResult<BranchResponse>.Success(ToResponse(branch));
    }

    public async Task<BranchTerminalResult<IReadOnlyList<TerminalResponse>>> GetTerminalsAsync(
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var authorizationError = await AuthorizeAsync("terminals.view", cancellationToken);
        if (authorizationError is not null)
        {
            return BranchTerminalResult<IReadOnlyList<TerminalResponse>>.Failure(authorizationError.Value, AuthorizationMessage(authorizationError.Value));
        }

        var organizationId = currentUser.OrganizationId!.Value;
        var branchExists = await dbContext.Branches.AnyAsync(
            branch => branch.Id == branchId && branch.OrganizationId == organizationId,
            cancellationToken);
        if (!branchExists)
        {
            return BranchTerminalResult<IReadOnlyList<TerminalResponse>>.Failure(BranchTerminalError.NotFound, "Branch not found.");
        }

        var terminals = await dbContext.Terminals
            .Where(terminal => terminal.BranchId == branchId && terminal.OrganizationId == organizationId)
            .OrderBy(terminal => terminal.Code)
            .ToListAsync(cancellationToken);

        return BranchTerminalResult<IReadOnlyList<TerminalResponse>>.Success(terminals.Select(ToResponse).ToList());
    }

    public async Task<BranchTerminalResult<TerminalResponse>> CreateTerminalAsync(
        Guid branchId,
        CreateTerminalRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorizationError = await AuthorizeAsync("terminals.create", cancellationToken);
        if (authorizationError is not null)
        {
            return BranchTerminalResult<TerminalResponse>.Failure(authorizationError.Value, AuthorizationMessage(authorizationError.Value));
        }

        if (request is null)
        {
            return BranchTerminalResult<TerminalResponse>.Failure(BranchTerminalError.Invalid, "A terminal request is required.");
        }

        var organizationId = currentUser.OrganizationId!.Value;
        var branch = await dbContext.Branches.SingleOrDefaultAsync(
            candidate => candidate.Id == branchId && candidate.OrganizationId == organizationId,
            cancellationToken);
        if (branch is null)
        {
            return BranchTerminalResult<TerminalResponse>.Failure(BranchTerminalError.NotFound, "Branch not found.");
        }

        if (!branch.IsActive)
        {
            return BranchTerminalResult<TerminalResponse>.Failure(BranchTerminalError.Conflict, "An inactive branch cannot accept terminals.");
        }

        Terminal terminal;
        try
        {
            terminal = new Terminal(organizationId, branch.Id, request.Code, request.Name);
        }
        catch (DomainException exception)
        {
            return BranchTerminalResult<TerminalResponse>.Failure(BranchTerminalError.Invalid, exception.Message);
        }

        if (await dbContext.Terminals.AnyAsync(existing =>
                existing.BranchId == branch.Id && existing.Code == terminal.Code,
                cancellationToken))
        {
            return BranchTerminalResult<TerminalResponse>.Failure(BranchTerminalError.Conflict, "A terminal with this code already exists in the branch.");
        }

        dbContext.Terminals.Add(terminal);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return BranchTerminalResult<TerminalResponse>.Failure(BranchTerminalError.Conflict, "A terminal with this code already exists in the branch.");
        }

        return BranchTerminalResult<TerminalResponse>.Success(ToResponse(terminal));
    }

    private async Task<BranchTerminalError?> AuthorizeAsync(string permission, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty ||
            currentUser.OrganizationId is not Guid organizationId || organizationId == Guid.Empty)
        {
            return BranchTerminalError.Unauthenticated;
        }

        return await permissionChecker.HasPermissionAsync(userId, permission, cancellationToken)
            ? null
            : BranchTerminalError.Forbidden;
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }

    private static string AuthorizationMessage(BranchTerminalError error)
    {
        return error == BranchTerminalError.Unauthenticated
            ? "Authentication is required."
            : "The required permission is missing.";
    }

    private static BranchResponse ToResponse(Branch branch)
    {
        return new BranchResponse(branch.Id, branch.Code, branch.Name, branch.IsActive, branch.CreatedAt, branch.UpdatedAt);
    }

    private static TerminalResponse ToResponse(Terminal terminal)
    {
        return new TerminalResponse(
            terminal.Id,
            terminal.BranchId,
            terminal.Code,
            terminal.Name,
            terminal.IsActive,
            terminal.CreatedAt,
            terminal.UpdatedAt,
            terminal.LastSeenAt);
    }
}