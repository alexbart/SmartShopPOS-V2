using SmartShopPOS.Contracts.Authentication;

namespace SmartShopPOS.Application.Identity;

public interface IAuthenticationService
{
    Task<AuthenticatedUserResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthenticatedUserResponse?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    Task<bool> ValidateSessionTokenAsync(string rawToken, CancellationToken cancellationToken = default);
}
