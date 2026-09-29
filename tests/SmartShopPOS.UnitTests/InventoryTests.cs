using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.UnitTests;

public sealed class InventoryTests
{
    [Fact]
    public void InventoryBalance_CreatedWithValidParameters()
    {
        var balance = new InventoryBalance(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m);

        Assert.Equal(100m, balance.QuantityOnHand);
        Assert.True(balance.Id != Guid.Empty);
        Assert.Equal(DateTimeOffset.UtcNow.Date, balance.CreatedAt.Date);
        Assert.Equal(DateTimeOffset.UtcNow.Date, balance.UpdatedAt.Date);
    }

    [Fact]
    public void InventoryBalance_RejectsNegativeQuantity()
    {
        Assert.Throws<DomainException>(() => new InventoryBalance(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            -1m));
    }

    [Fact]
    public void InventoryBalance_RejectsEmptyOrganizationId()
    {
        Assert.Throws<DomainException>(() => new InventoryBalance(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m));
    }

    [Fact]
    public void InventoryBalance_RejectsEmptyBranchId()
    {
        Assert.Throws<DomainException>(() => new InventoryBalance(
            Guid.NewGuid(),
            Guid.Empty,
            Guid.NewGuid(),
            100m));
    }

    [Fact]
    public void InventoryBalance_RejectsEmptyProductId()
    {
        Assert.Throws<DomainException>(() => new InventoryBalance(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Empty,
            100m));
    }

    [Fact]
    public void InventoryBalance_AdjustQuantity_Increases()
    {
        var balance = new InventoryBalance(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m);

        balance.AdjustQuantity(50m);

        Assert.Equal(150m, balance.QuantityOnHand);
        Assert.Equal(DateTimeOffset.UtcNow.Date, balance.UpdatedAt.Date);
    }

    [Fact]
    public void InventoryBalance_AdjustQuantity_Decreases()
    {
        var balance = new InventoryBalance(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m);

        balance.AdjustQuantity(-30m);

        Assert.Equal(70m, balance.QuantityOnHand);
    }

    [Fact]
    public void InventoryBalance_AdjustQuantity_RejectsNegativeResult()
    {
        var balance = new InventoryBalance(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m);

        Assert.Throws<DomainException>(() => balance.AdjustQuantity(-150m));
        Assert.Equal(100m, balance.QuantityOnHand);
    }

    [Fact]
    public void InventoryBalance_SupportsFractionalQuantities()
    {
        var balance = new InventoryBalance(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            2.5m);

        Assert.Equal(2.5m, balance.QuantityOnHand);

        balance.AdjustQuantity(0.75m);
        Assert.Equal(3.25m, balance.QuantityOnHand);

        balance.AdjustQuantity(-1.25m);
        Assert.Equal(2.0m, balance.QuantityOnHand);
    }

    [Fact]
    public void StockMovement_CreatedWithValidParameters()
    {
        var movement = new StockMovement(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            MovementType.Receipt,
            50m,
            DateTimeOffset.UtcNow,
            "PURCHASE_RECEIPT",
            Guid.NewGuid(),
            "Initial stock receipt",
            Guid.NewGuid());

        Assert.Equal(MovementType.Receipt, movement.MovementType);
        Assert.Equal(50m, movement.Quantity);
        Assert.Equal("PURCHASE_RECEIPT", movement.ReferenceType);
        Assert.Equal("Initial stock receipt", movement.Reason);
        Assert.True(movement.Id != Guid.Empty);
    }

    [Fact]
    public void StockMovement_RejectsZeroQuantity()
    {
        Assert.Throws<DomainException>(() => new StockMovement(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            MovementType.Receipt,
            0m,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void StockMovement_RejectsNegativeQuantity()
    {
        Assert.Throws<DomainException>(() => new StockMovement(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            MovementType.Receipt,
            -10m,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void StockMovement_RejectsInvalidMovementType()
    {
        Assert.Throws<DomainException>(() => new StockMovement(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), (MovementType)99, 1m, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void StockMovement_RejectsQuantityBeyondDatabasePrecision()
    {
        Assert.Throws<DomainException>(() => new StockMovement(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), MovementType.Receipt, 1.00001m, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void StockMovement_RejectsOverlongReason()
    {
        Assert.Throws<DomainException>(() => new StockMovement(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), MovementType.Receipt, 1m,
            DateTimeOffset.UtcNow, reason: new string('x', 501)));
    }

    [Fact]
    public void StockMovement_RejectsEmptyOrganizationId()
    {
        Assert.Throws<DomainException>(() => new StockMovement(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MovementType.Receipt,
            10m,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void StockMovement_GetDelta_ReturnsCorrectDirection()
    {
        Assert.Equal(1m, StockMovement.GetDelta(MovementType.Receipt));
        Assert.Equal(-1m, StockMovement.GetDelta(MovementType.Sale));
        Assert.Equal(1m, StockMovement.GetDelta(MovementType.AdjustmentIncrease));
        Assert.Equal(-1m, StockMovement.GetDelta(MovementType.AdjustmentDecrease));
        Assert.Equal(1m, StockMovement.GetDelta(MovementType.OpeningBalance));
        Assert.Equal(1m, StockMovement.GetDelta(MovementType.Return));
    }

    [Fact]
    public void MovementType_HasAllRequiredValues()
    {
        var values = Enum.GetValues<MovementType>();

        Assert.Contains(MovementType.Receipt, values);
        Assert.Contains(MovementType.Sale, values);
        Assert.Contains(MovementType.AdjustmentIncrease, values);
        Assert.Contains(MovementType.AdjustmentDecrease, values);
        Assert.Contains(MovementType.OpeningBalance, values);
        Assert.Contains(MovementType.Return, values);
        Assert.Equal(6, values.Length);
    }
}
