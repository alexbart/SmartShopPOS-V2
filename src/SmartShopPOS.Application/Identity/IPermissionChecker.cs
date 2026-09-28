namespace SmartShopPOS.Application.Identity;

public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken cancellationToken = default);
}