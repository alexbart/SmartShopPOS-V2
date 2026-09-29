using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.UnitTests;

public sealed class BranchTerminalTests
{
    [Fact]
    public void Branch_NormalizesCodeAndCanBeDeactivated()
    {
        var branch = new Branch(Guid.NewGuid(), "nai-001", "  Nairobi CBD  ");

        Assert.Equal("NAI-001", branch.Code);
        Assert.Equal("Nairobi CBD", branch.Name);
        Assert.True(branch.IsActive);

        branch.SetActive(false);

        Assert.False(branch.IsActive);
    }

    [Fact]
    public void Branch_RejectsInvalidTenantCodeAndName()
    {
        Assert.Throws<DomainException>(() => new Branch(Guid.Empty, "NAI-001", "Nairobi"));
        Assert.Throws<DomainException>(() => new Branch(Guid.NewGuid(), "NAI--001", "Nairobi"));
        Assert.Throws<DomainException>(() => new Branch(Guid.NewGuid(), "NAI-001", " "));
    }

    [Fact]
    public void Terminal_NormalizesCodeTracksLastSeenAndCanBeDeactivated()
    {
        var terminal = new Terminal(Guid.NewGuid(), Guid.NewGuid(), "pos-01", "Counter 1");
        var seenAt = DateTimeOffset.Parse("2026-09-28T12:00:00+03:00");

        terminal.MarkSeen(seenAt);
        terminal.SetActive(false);

        Assert.Equal("POS-01", terminal.Code);
        Assert.Equal(seenAt.ToUniversalTime(), terminal.LastSeenAt);
        Assert.False(terminal.IsActive);
    }

    [Fact]
    public void Terminal_RequiresTenantAndBranchAndValidCode()
    {
        Assert.Throws<DomainException>(() => new Terminal(Guid.Empty, Guid.NewGuid(), "POS-01", "Counter 1"));
        Assert.Throws<DomainException>(() => new Terminal(Guid.NewGuid(), Guid.Empty, "POS-01", "Counter 1"));
        Assert.Throws<DomainException>(() => new Terminal(Guid.NewGuid(), Guid.NewGuid(), "POS--01", "Counter 1"));
    }
}