namespace SmartShopPOS.Contracts.Purchasing;

public sealed record PurchaseOrderLineUpsertRequest(
    Guid ProductId,
    decimal Quantity,
    decimal UnitCost,
    string? Notes);

public sealed record PurchaseOrderLineResponse(
    Guid Id,
    Guid PurchaseOrderId,
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal Quantity,
    decimal UnitCost,
    decimal LineTotal,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
