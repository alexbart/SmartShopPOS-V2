namespace SmartShopPOS.Domain.Identity;

public sealed class PurchaseOrderLine : Entity
{
    private PurchaseOrderLine()
    {
    }

    public PurchaseOrderLine(
        Guid organizationId,
        Guid purchaseOrderId,
        Guid productId,
        decimal quantity,
        decimal unitCost,
        string? notes = null)
    {
        if (organizationId == Guid.Empty || purchaseOrderId == Guid.Empty || productId == Guid.Empty)
        {
            throw new DomainException("A purchase order line requires organization, purchase order, and product identifiers.");
        }

        OrganizationId = organizationId;
        PurchaseOrderId = purchaseOrderId;
        SetValues(quantity, unitCost, notes);
        ProductId = productId;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid OrganizationId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal LineTotal => Quantity * UnitCost;
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public PurchaseOrder PurchaseOrder { get; private set; } = null!;
    public Product Product { get; private set; } = null!;

    public void Update(Guid productId, decimal quantity, decimal unitCost, string? notes)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("A purchase order line requires a product.");
        }

        SetValues(quantity, unitCost, notes);
        ProductId = productId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void SetValues(decimal quantity, decimal unitCost, string? notes)
    {
        if (quantity <= 0m)
        {
            throw new DomainException("Line quantity must be greater than zero.");
        }

        if (quantity != decimal.Round(quantity, 4) || quantity >= 100_000_000_000_000m)
        {
            throw new DomainException("Line quantity must fit numeric(18,4).");
        }

        if (unitCost < 0m)
        {
            throw new DomainException("Unit cost cannot be negative.");
        }

        if (unitCost != decimal.Round(unitCost, 4) || unitCost >= 100_000_000_000_000m)
        {
            throw new DomainException("Unit cost must fit numeric(18,4).");
        }

        var normalizedNotes = notes?.Trim();
        if (normalizedNotes?.Length > 1000)
        {
            throw new DomainException("Line notes must not exceed 1000 characters.");
        }

        Quantity = quantity;
        UnitCost = unitCost;
        Notes = string.IsNullOrWhiteSpace(normalizedNotes) ? null : normalizedNotes;
    }
}
