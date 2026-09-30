using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.UnitTests;

public sealed class PurchaseOrderLineTests
{
    [Fact]
    public void PurchaseOrderLine_AcceptsFractionalQuantityAndCalculatesLineTotal()
    {
        var line = new PurchaseOrderLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2.5m, 4.125m, "  bulk pack  ");

        Assert.Equal(2.5m, line.Quantity);
        Assert.Equal(4.125m, line.UnitCost);
        Assert.Equal(10.3125m, line.LineTotal);
        Assert.Equal("bulk pack", line.Notes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PurchaseOrderLine_RejectsNonPositiveQuantity(decimal quantity) =>
        Assert.Throws<DomainException>(() => new PurchaseOrderLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), quantity, 1m));

    [Fact]
    public void PurchaseOrderLine_AllowsZeroCostButRejectsNegativeOrUnrepresentableValues()
    {
        var free = new PurchaseOrderLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m, 0m);
        Assert.Equal(0m, free.LineTotal);
        Assert.Throws<DomainException>(() => new PurchaseOrderLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m, -0.01m));
        Assert.Throws<DomainException>(() => new PurchaseOrderLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1.00001m, 1m));
    }

    [Fact]
    public void PurchaseOrderLine_UpdateChangesOnlyProductAndLineValues()
    {
        var line = new PurchaseOrderLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m, 2m);
        var id = line.Id;
        var createdAt = line.CreatedAt;
        var purchaseOrderId = line.PurchaseOrderId;
        var organizationId = line.OrganizationId;
        var originalProductId = line.ProductId;

        Assert.Throws<DomainException>(() => line.Update(Guid.NewGuid(), 0m, 2m, "invalid"));
        Assert.Equal(originalProductId, line.ProductId);
        Assert.Equal(1m, line.Quantity);

        line.Update(Guid.NewGuid(), 3.25m, 1.5m, "updated");

        Assert.Equal(id, line.Id);
        Assert.Equal(purchaseOrderId, line.PurchaseOrderId);
        Assert.Equal(organizationId, line.OrganizationId);
        Assert.Equal(createdAt, line.CreatedAt);
        Assert.Equal(3.25m, line.Quantity);
        Assert.Equal(1.5m, line.UnitCost);
        Assert.Equal("updated", line.Notes);
        Assert.True(line.UpdatedAt >= createdAt);
    }
}
