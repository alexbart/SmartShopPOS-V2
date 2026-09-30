using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.UnitTests;

public sealed class GoodsReceiptTests
{
    [Fact]
    public void ReceiptLine_AllowsFractionalQuantityAndRejectsNonPositiveValues()
    {
        var line = new GoodsReceiptLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0.75m);
        Assert.Equal(0.75m, line.QuantityReceived);
        Assert.Throws<DomainException>(() => new GoodsReceiptLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m));
        Assert.Throws<DomainException>(() => new GoodsReceiptLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -1m));
        Assert.Throws<DomainException>(() => new GoodsReceiptLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1.00001m));
    }

    [Fact]
    public void GoodsReceipt_NormalizesTimestampAndNotesAndHasNoMutationSurface()
    {
        var created = new GoodsReceipt(Guid.NewGuid(), Guid.NewGuid(), " GR-ABC ",
            new DateTimeOffset(2026, 9, 30, 13, 0, 0, TimeSpan.FromHours(3)), " received ", Guid.NewGuid());
        Assert.Equal("GR-ABC", created.ReceiptNumber);
        Assert.Equal("received", created.Notes);
        Assert.Equal(TimeSpan.Zero, created.ReceivedAt.Offset);
        Assert.DoesNotContain(typeof(GoodsReceipt).GetMethods(), method => method.Name is "Update" or "Delete");
    }
}
