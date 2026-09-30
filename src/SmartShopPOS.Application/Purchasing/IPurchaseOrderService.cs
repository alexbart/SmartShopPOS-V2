using SmartShopPOS.Contracts.Purchasing;

namespace SmartShopPOS.Application.Purchasing;

public interface IPurchaseOrderService
{
    Task<PurchaseOrderResult<IReadOnlyList<PurchaseOrderSummaryResponse>>> ListAsync(CancellationToken cancellationToken = default);
    Task<PurchaseOrderResult<PurchaseOrderResponse>> GetAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<PurchaseOrderResult<PurchaseOrderResponse>> CreateAsync(PurchaseOrderUpsertRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderResult<PurchaseOrderResponse>> UpdateAsync(Guid purchaseOrderId, PurchaseOrderUpsertRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderResult<PurchaseOrderResponse>> SubmitAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<PurchaseOrderResult<PurchaseOrderResponse>> CancelAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
}

public enum PurchaseOrderError { None, Unauthenticated, Forbidden, NotFound, Conflict, Invalid, InactiveSupplier, InactiveBranch }

public sealed record PurchaseOrderResult<T>(T? Value, PurchaseOrderError Error, string? Message)
{
    public bool IsSuccess => Error == PurchaseOrderError.None;
    public static PurchaseOrderResult<T> Success(T value) => new(value, PurchaseOrderError.None, null);
    public static PurchaseOrderResult<T> Failure(PurchaseOrderError error, string message) => new(default, error, message);
}
