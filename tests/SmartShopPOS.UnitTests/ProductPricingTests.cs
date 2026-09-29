using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.UnitTests;

public sealed class ProductPricingTests
{
    [Fact]
    public void ProductPrice_RequiresNonNegativeMonetaryValuesAndValidInterval()
    {
        var organizationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var effectiveFrom = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<DomainException>(() => new ProductPrice(organizationId, Guid.Empty, 100m, 150m, effectiveFrom));
        Assert.Throws<DomainException>(() => new ProductPrice(Guid.Empty, productId, 100m, 150m, effectiveFrom));
        Assert.Throws<DomainException>(() => new ProductPrice(organizationId, productId, -1m, 150m, effectiveFrom));
        Assert.Throws<DomainException>(() => new ProductPrice(organizationId, productId, 100m, -1m, effectiveFrom));

        var valid = new ProductPrice(organizationId, productId, 100m, 150m, effectiveFrom);
        Assert.Equal(100m, valid.CostPrice);
        Assert.Equal(150m, valid.SellingPrice);
        Assert.True(valid.IsEffectiveAt(effectiveFrom));

        var closedAt = effectiveFrom.AddDays(30);
        valid.CloseAt(closedAt);
        Assert.Equal(closedAt, valid.EffectiveTo);
    }

    [Fact]
    public void ProductPrice_IdentifiesTheEffectivePriceAtAPointInTime()
    {
        var organizationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var first = new ProductPrice(organizationId, productId, 100m, 150m, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var second = new ProductPrice(organizationId, productId, 110m, 180m, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));

        first.CloseAt(second.EffectiveFrom);

        Assert.True(first.IsEffectiveAt(new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero)));
        Assert.False(first.IsEffectiveAt(second.EffectiveFrom));
        Assert.True(second.IsEffectiveAt(new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero)));
    }
}
