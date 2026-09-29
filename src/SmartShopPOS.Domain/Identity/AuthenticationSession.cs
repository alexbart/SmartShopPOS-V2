namespace SmartShopPOS.Domain.Identity;

public sealed class AuthenticationSession : Entity
{
    private AuthenticationSession()
    {
    }

    public AuthenticationSession(
        Guid userId,
        Guid organizationId,
        Guid sessionId,
        string tokenHash,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? expiresAt = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A session must reference a user.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A session must reference an organization.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("A session token hash is required.");
        }

        UserId = userId;
        OrganizationId = organizationId;
        SessionId = sessionId == Guid.Empty ? Guid.NewGuid() : sessionId;
        TokenHash = tokenHash;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        ExpiresAt = expiresAt ?? CreatedAt.AddMinutes(60);
        LastUsedAt = CreatedAt;
    }

    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid? SelectedBranchId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }

    public User User { get; private set; } = null!;
    public Organization Organization { get; private set; } = null!;

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsExpired(DateTimeOffset utcNow)
    {
        return IsRevoked || utcNow >= ExpiresAt;
    }

    public void Touch(DateTimeOffset utcNow)
    {
        LastUsedAt = utcNow;
    }

    public void Revoke()
    {
        if (!IsRevoked)
        {
            RevokedAt = DateTimeOffset.UtcNow;
            SelectedBranchId = null;
        }
    }

    public void SetSelectedBranch(Guid? branchId)
    {
        if (branchId == Guid.Empty)
        {
            throw new DomainException("A selected branch identifier cannot be empty.");
        }

        SelectedBranchId = branchId;
    }
}
