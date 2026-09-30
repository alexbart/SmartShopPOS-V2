namespace SmartShopPOS.Contracts.Purchasing;

public sealed record PurchaseOrderUpsertRequest(
    Guid SupplierId,
    Guid BranchId,
    DateTimeOffset OrderDate,
    DateTimeOffset? ExpectedDate,
    string? Notes);

public sealed record PurchaseOrderSummaryResponse(
    Guid Id,
    string OrderNumber,
    Guid SupplierId,
    string SupplierName,
    Guid BranchId,
    string BranchName,
    string Status,
    DateTimeOffset OrderDate,
    DateTimeOffset? ExpectedDate,
    DateTimeOffset CreatedAt);

public sealed record PurchaseOrderResponse(
    Guid Id,
    string OrderNumber,
    Guid SupplierId,
    string SupplierName,
    Guid BranchId,
    string BranchName,
    string Status,
    DateTimeOffset OrderDate,
    DateTimeOffset? ExpectedDate,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid CreatedByUserId,
    Guid? UpdatedByUserId);
