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
    DateTimeOffset UpdatedAt,
    decimal ReceivedQuantity,
    decimal RemainingQuantity,
    bool IsFullyReceived);

public sealed record PurchaseOrderReceivingSummaryResponse(
    Guid PurchaseOrderId,
    string OrderNumber,
    string Status,
    string ReceivingState,
    decimal OrderedQuantity,
    decimal ReceivedQuantity,
    decimal RemainingQuantity,
    IReadOnlyList<PurchaseOrderLineResponse> Lines);
