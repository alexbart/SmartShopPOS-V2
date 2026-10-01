using SmartShopPOS.Contracts.Purchasing;

namespace SmartShopPOS.Application.Purchasing;

public interface IPurchaseOrderLineService
{
    Task<PurchaseOrderLineResult<IReadOnlyList<PurchaseOrderLineResponse>>> ListAsync(
        Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<PurchaseOrderLineResult<PurchaseOrderReceivingSummaryResponse>> GetReceivingSummaryAsync(
        Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<PurchaseOrderLineResult<PurchaseOrderLineResponse>> CreateAsync(
        Guid purchaseOrderId, PurchaseOrderLineUpsertRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderLineResult<PurchaseOrderLineResponse>> UpdateAsync(
        Guid purchaseOrderId, Guid lineId, PurchaseOrderLineUpsertRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderLineResult<bool>> DeleteAsync(
        Guid purchaseOrderId, Guid lineId, CancellationToken cancellationToken = default);
}

public enum PurchaseOrderLineError
{
    None,
    Unauthenticated,
    Forbidden,
    NotFound,
    Conflict,
    Invalid,
    InactiveProduct
}

public sealed record PurchaseOrderLineResult<T>(T? Value, PurchaseOrderLineError Error, string? Message)
{
    public bool IsSuccess => Error == PurchaseOrderLineError.None;
    public static PurchaseOrderLineResult<T> Success(T value) => new(value, PurchaseOrderLineError.None, null);
    public static PurchaseOrderLineResult<T> Failure(PurchaseOrderLineError error, string message) => new(default, error, message);
}
