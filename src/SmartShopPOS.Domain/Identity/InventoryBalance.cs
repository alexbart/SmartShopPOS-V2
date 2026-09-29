namespace SmartShopPOS.Domain.Identity;

public sealed class InventoryBalance : Entity
{
    private InventoryBalance()
    {
    }

    public InventoryBalance(
        Guid organizationId,
        Guid branchId,
        Guid productId,
        decimal quantityOnHand)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("An inventory balance must belong to an organization.");
        }

        if (branchId == Guid.Empty)
        {
            throw new DomainException("An inventory balance must belong to a branch.");
        }

        if (productId == Guid.Empty)
        {
            throw new DomainException("An inventory balance must belong to a product.");
        }

        if (quantityOnHand < 0m)
        {
            throw new DomainException("Quantity on hand cannot be negative.");
        }

        if (quantityOnHand != decimal.Round(quantityOnHand, 4) || quantityOnHand >= 100_000_000_000_000m)
        {
            throw new DomainException("Quantity on hand must fit numeric(18,4).");
        }

        OrganizationId = organizationId;
        BranchId = branchId;
        ProductId = productId;
        QuantityOnHand = quantityOnHand;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid OrganizationId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal QuantityOnHand { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Organization Organization { get; private set; } = null!;
    public Branch Branch { get; private set; } = null!;
    public Product Product { get; private set; } = null!;

    public void AdjustQuantity(decimal delta)
    {
        var newQuantity = QuantityOnHand + delta;
        if (newQuantity != decimal.Round(newQuantity, 4) || newQuantity >= 100_000_000_000_000m)
        {
            throw new DomainException("Quantity on hand must fit numeric(18,4).");
        }
        if (newQuantity < 0m)
        {
            throw new DomainException("Insufficient stock.");
        }

        QuantityOnHand = newQuantity;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
