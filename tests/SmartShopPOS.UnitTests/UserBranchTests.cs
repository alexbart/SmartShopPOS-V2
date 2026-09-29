using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.UnitTests;

public sealed class UserBranchTests
{
    [Fact]
    public void UserBranchAssignment_CanBeDeactivatedWithoutDeletingHistory()
    {
        var assignment = new UserBranch(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var createdAt = assignment.CreatedAt;

        Assert.True(assignment.IsActive);
        Assert.Null(assignment.DeactivatedAt);

        assignment.Deactivate();

        Assert.False(assignment.IsActive);
        Assert.NotNull(assignment.DeactivatedAt);
        Assert.True(assignment.DeactivatedAt >= createdAt);
    }

    [Fact]
    public void UserBranchAssignment_RequiresOrganizationUserAndBranch()
    {
        Assert.Throws<DomainException>(() => new UserBranch(Guid.Empty, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new UserBranch(Guid.NewGuid(), Guid.Empty, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new UserBranch(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void AuthenticationSession_ReplacesAndRevocationClearsSelectedBranch()
    {
        var session = new AuthenticationSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "token-hash");
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();

        session.SetSelectedBranch(branchA);
        Assert.Equal(branchA, session.SelectedBranchId);

        session.SetSelectedBranch(branchB);
        Assert.Equal(branchB, session.SelectedBranchId);

        session.Revoke();
        Assert.True(session.IsRevoked);
        Assert.Null(session.SelectedBranchId);
    }
}