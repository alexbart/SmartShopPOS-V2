using System.Security.Cryptography;
using System.Text;

namespace SmartShopPOS.Infrastructure.Identity;

public static class AuthenticationSecurity
{
    public static string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("A session token is required.", nameof(token));
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
