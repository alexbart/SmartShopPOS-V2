namespace SmartShopPOS.Contracts.Purchasing;

public sealed record CreateGoodsReceiptRequest(DateTimeOffset ReceivedAt, string? Notes, IReadOnlyList<CreateGoodsReceiptLineRequest> Lines);
public sealed record CreateGoodsReceiptLineRequest(Guid PurchaseOrderLineId, decimal QuantityReceived);
public sealed record GoodsReceiptLineResponse(Guid Id, Guid PurchaseOrderLineId, Guid ProductId, string ProductName, decimal QuantityReceived, decimal TotalReceivedForLine);
public sealed record GoodsReceiptResponse(Guid Id, string ReceiptNumber, Guid PurchaseOrderId, string PurchaseOrderNumber,
    Guid SupplierId, string SupplierName, Guid BranchId, string BranchName, DateTimeOffset ReceivedAt, string? Notes,
    Guid CreatedByUserId, DateTimeOffset CreatedAt, IReadOnlyList<GoodsReceiptLineResponse> Lines);
