using SmartShopPOS.Contracts.Inventory;

namespace SmartShopPOS.Application.Inventory;

public interface IInventoryService
{
    Task<InventoryResult<IReadOnlyList<InventoryBalanceResponse>>> ListBalancesAsync(
        InventoryFilter filter,
        CancellationToken cancellationToken = default);

    Task<InventoryResult<InventoryBalanceResponse?>> GetBalanceAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<InventoryResult<IReadOnlyList<StockMovementResponse>>> ListMovementsAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<InventoryResult<InventoryBalanceResponse>> CreateOpeningBalanceAsync(
        Guid productId,
        OpeningBalanceRequest request,
        CancellationToken cancellationToken = default);

    Task<InventoryResult<InventoryBalanceResponse>> CreateAdjustmentIncreaseAsync(
        Guid productId,
        AdjustmentRequest request,
        CancellationToken cancellationToken = default);

    Task<InventoryResult<InventoryBalanceResponse>> CreateAdjustmentDecreaseAsync(
        Guid productId,
        AdjustmentRequest request,
        CancellationToken cancellationToken = default);
}

public readonly record struct InventoryResult<T>(T? Value, InventoryError Error, string? Message, bool IsSuccess)
{
    public static InventoryResult<T> Success(T value) => new(value, default, null, true);
    public static InventoryResult<T> Failure(InventoryError error, string message) => new(default, error, message, false);
}
