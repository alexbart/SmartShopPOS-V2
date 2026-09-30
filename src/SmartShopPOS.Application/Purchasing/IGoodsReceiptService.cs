using SmartShopPOS.Contracts.Purchasing;

namespace SmartShopPOS.Application.Purchasing;

public interface IGoodsReceiptService
{
    Task<GoodsReceiptResult<IReadOnlyList<GoodsReceiptResponse>>> ListForOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<GoodsReceiptResult<GoodsReceiptResponse>> CreateAsync(Guid purchaseOrderId, CreateGoodsReceiptRequest request, string? idempotencyKey, CancellationToken cancellationToken = default);
    Task<GoodsReceiptResult<GoodsReceiptResponse>> GetAsync(Guid receiptId, CancellationToken cancellationToken = default);
}

public enum GoodsReceiptError { None, Unauthenticated, Forbidden, NotFound, Conflict, Invalid }
public sealed record GoodsReceiptResult<T>(T? Value, GoodsReceiptError Error, string? Message, bool WasReplay = false)
{
    public bool IsSuccess => Error == GoodsReceiptError.None;
    public static GoodsReceiptResult<T> Success(T value, bool replay = false) => new(value, GoodsReceiptError.None, null, replay);
    public static GoodsReceiptResult<T> Failure(GoodsReceiptError error, string message) => new(default, error, message);
}
