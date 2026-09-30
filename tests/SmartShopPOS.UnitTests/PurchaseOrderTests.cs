using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.UnitTests;

public sealed class PurchaseOrderTests
{
    private static PurchaseOrder Create() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-000001",
        DateTimeOffset.UtcNow, null, "  initial order  ", Guid.NewGuid());

    [Fact]
    public void PurchaseOrder_StartsDraftAndNormalizesNotesAndDates()
    {
        var order = Create();
        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
        Assert.Equal("initial order", order.Notes);
        Assert.Equal(TimeSpan.Zero, order.OrderDate.Offset);
    }

    [Fact]
    public void PurchaseOrder_AllowsDraftUpdateSubmitThenCancel()
    {
        var order = Create();
        var updater = Guid.NewGuid();
        order.UpdateDraft(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1), null, null, updater);
        order.Submit(updater);
        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);
        order.Cancel(updater);
        Assert.Equal(PurchaseOrderStatus.Cancelled, order.Status);
        Assert.Equal(updater, order.UpdatedByUserId);
    }

    [Fact]
    public void PurchaseOrder_RejectsEditingAfterSubmitAndSubmittingTwice()
    {
        var order = Create();
        order.Submit(Guid.NewGuid());
        Assert.Throws<DomainException>(() => order.UpdateDraft(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, null, null, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => order.Submit(Guid.NewGuid()));
    }

    [Fact]
    public void PurchaseOrder_RejectsCancelledResubmissionOrRepeatedCancellation()
    {
        var order = Create();
        order.Cancel(Guid.NewGuid());
        Assert.Throws<DomainException>(() => order.Submit(Guid.NewGuid()));
        Assert.Throws<DomainException>(() => order.Cancel(Guid.NewGuid()));
    }

    [Fact]
    public void PurchaseOrder_RejectsInvalidRequiredDataAndLongNotes()
    {
        var organizationId = Guid.NewGuid();
        Assert.Throws<DomainException>(() => new PurchaseOrder(organizationId, Guid.Empty, Guid.NewGuid(), "PO-1", DateTimeOffset.UtcNow, null, null, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new PurchaseOrder(organizationId, Guid.NewGuid(), Guid.NewGuid(), "PO-1", default, null, null, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new PurchaseOrder(organizationId, Guid.NewGuid(), Guid.NewGuid(), "PO-1", DateTimeOffset.UtcNow, null, new string('x', 1001), Guid.NewGuid()));
    }
}
