using SmartShopPOS.Application.Identity;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Identity;

namespace SmartShopPOS.UnitTests;

public class AuthenticationTests
{
    [Fact]
    public void PasswordHasher_GeneratesHashAndVerifiesMatchingPassword()
    {
        var hasher = new Pbkdf2PasswordHasher();

        var hash = hasher.HashPassword("P@ssw0rd!");

        Assert.NotEqual("P@ssw0rd!", hash);
        Assert.True(hasher.VerifyPassword("P@ssw0rd!", hash));
        Assert.False(hasher.VerifyPassword("WrongPassword!", hash));
    }

    [Fact]
    public void Session_TracksExpirationAndRevocationState()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new AuthenticationSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hash-value");

        Assert.False(session.IsRevoked);
        Assert.False(session.IsExpired(now.AddMinutes(30)));

        session.Revoke();
        Assert.True(session.IsRevoked);

        var expired = new AuthenticationSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hash-value", now.AddMinutes(-5), now.AddMinutes(-1));
        Assert.True(expired.IsExpired(now));
    }
}
