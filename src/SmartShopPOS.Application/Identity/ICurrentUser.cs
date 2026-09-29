namespace SmartShopPOS.Application.Identity;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? OrganizationId { get; }
    Guid? SessionId { get; }
    string? Email { get; }
}
