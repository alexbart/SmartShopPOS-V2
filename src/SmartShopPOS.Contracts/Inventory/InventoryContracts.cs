namespace SmartShopPOS.Contracts.Inventory;

public sealed record InventoryBalanceResponse(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    decimal QuantityOnHand,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record StockMovementResponse(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    int MovementType,
    string MovementTypeName,
    decimal Quantity,
    decimal Delta,
    DateTimeOffset OccurredAt,
    string? ReferenceType,
    Guid? ReferenceId,
    string? Reason,
    Guid? CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record OpeningBalanceRequest(decimal Quantity, string Reason);

public sealed record AdjustmentRequest(decimal Quantity, string Reason);

public sealed record InventoryFilter(
    bool? Active = null,
    string? Sku = null,
    string? Barcode = null,
    string? Search = null);

public enum InventoryError
{
    Unauthenticated,
    Forbidden,
    NotFound,
    Conflict,
    Invalid,
    InsufficientStock,
    NoBranchContext,
    InactiveBranch,
    InactiveProduct,
    BranchAssignmentRevoked
}