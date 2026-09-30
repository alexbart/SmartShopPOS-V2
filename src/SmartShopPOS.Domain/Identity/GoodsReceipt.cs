namespace SmartShopPOS.Domain.Identity;

public sealed class GoodsReceipt : Entity
{
    private GoodsReceipt() { }

    public GoodsReceipt(Guid organizationId, Guid purchaseOrderId, string receiptNumber,
        DateTimeOffset receivedAt, string? notes, Guid createdByUserId, Guid? id = null)
    {
        if (organizationId == Guid.Empty || purchaseOrderId == Guid.Empty || createdByUserId == Guid.Empty)
            throw new DomainException("A goods receipt requires organization, purchase order, and creator identifiers.");
        var normalizedNumber = receiptNumber?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedNumber) || normalizedNumber.Length > 32)
            throw new DomainException("A valid receipt number is required.");
        var normalizedNotes = notes?.Trim();
        if (normalizedNotes?.Length > 1000)
            throw new DomainException("Receipt notes must not exceed 1000 characters.");
        if (receivedAt == default)
            throw new DomainException("ReceivedAt is required.");
        OrganizationId = organizationId;
        if (id is Guid receiptId && receiptId != Guid.Empty) Id = receiptId;
        PurchaseOrderId = purchaseOrderId;
        ReceiptNumber = normalizedNumber;
        ReceivedAt = receivedAt.ToUniversalTime();
        Notes = string.IsNullOrWhiteSpace(normalizedNotes) ? null : normalizedNotes;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid OrganizationId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public string ReceiptNumber { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }
}

public sealed class GoodsReceiptLine : Entity
{
    private GoodsReceiptLine() { }

    public GoodsReceiptLine(Guid organizationId, Guid goodsReceiptId, Guid purchaseOrderId,
        Guid purchaseOrderLineId, decimal quantityReceived)
    {
        if (organizationId == Guid.Empty || goodsReceiptId == Guid.Empty || purchaseOrderId == Guid.Empty || purchaseOrderLineId == Guid.Empty)
            throw new DomainException("A goods receipt line requires valid receipt and purchase order line identifiers.");
        if (quantityReceived <= 0m || quantityReceived != decimal.Round(quantityReceived, 4) || quantityReceived >= 100_000_000_000_000m)
            throw new DomainException("Received quantity must be positive and fit numeric(18,4).");
        OrganizationId = organizationId;
        GoodsReceiptId = goodsReceiptId;
        PurchaseOrderId = purchaseOrderId;
        PurchaseOrderLineId = purchaseOrderLineId;
        QuantityReceived = quantityReceived;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid OrganizationId { get; private set; }
    public Guid GoodsReceiptId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid PurchaseOrderLineId { get; private set; }
    public decimal QuantityReceived { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class GoodsReceiptIdempotency
{
    private GoodsReceiptIdempotency() { }
    public GoodsReceiptIdempotency(Guid organizationId, string key, string requestHash, Guid receiptId)
    {
        OrganizationId = organizationId;
        Key = key;
        RequestHash = requestHash;
        GoodsReceiptId = receiptId;
    }
    public Guid OrganizationId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public Guid GoodsReceiptId { get; private set; }
}

public sealed class GoodsReceiptNumberSequence
{
    private GoodsReceiptNumberSequence() { }
    public Guid OrganizationId { get; private set; }
    public long LastNumber { get; private set; }
    public Organization Organization { get; private set; } = null!;
}
