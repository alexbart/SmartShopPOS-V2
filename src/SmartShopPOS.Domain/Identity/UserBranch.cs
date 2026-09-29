namespace SmartShopPOS.Domain.Identity;

public sealed class UserBranch : Entity
{
    private UserBranch()
    {
    }

    public UserBranch(Guid organizationId, Guid userId, Guid branchId)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty || branchId == Guid.Empty)
        {
            throw new DomainException("A user branch assignment requires organization, user, and branch identifiers.");
        }

        OrganizationId = organizationId;
        UserId = userId;
        BranchId = branchId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid BranchId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }

    public bool IsActive => DeactivatedAt is null;

    public void Deactivate()
    {
        DeactivatedAt ??= DateTimeOffset.UtcNow;
    }
}