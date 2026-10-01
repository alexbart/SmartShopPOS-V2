using SmartShopPOS.Contracts.Purchasing;

namespace SmartShopPOS.Application.Purchasing;

public interface ISupplierInvoiceService
{
    Task<SupplierInvoiceResult<IReadOnlyList<SupplierInvoiceSummaryResponse>>> ListAsync(CancellationToken cancellationToken = default);
    Task<SupplierInvoiceResult<SupplierInvoiceResponse>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SupplierInvoiceResult<SupplierInvoiceResponse>> CreateAsync(SupplierInvoiceCreateRequest request, CancellationToken cancellationToken = default);
    Task<SupplierInvoiceResult<SupplierInvoiceResponse>> UpdateAsync(Guid id, SupplierInvoiceUpdateRequest request, CancellationToken cancellationToken = default);
    Task<SupplierInvoiceResult<SupplierInvoiceLineResponse>> AddLineAsync(Guid id, SupplierInvoiceLineRequest request, CancellationToken cancellationToken = default);
    Task<SupplierInvoiceResult<SupplierInvoiceLineResponse>> UpdateLineAsync(Guid id, Guid lineId, SupplierInvoiceLineRequest request, CancellationToken cancellationToken = default);
    Task<SupplierInvoiceResult<bool>> DeleteLineAsync(Guid id, Guid lineId, CancellationToken cancellationToken = default);
    Task<SupplierInvoiceResult<SupplierInvoiceResponse>> PostAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SupplierInvoiceResult<bool>> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}

public enum SupplierInvoiceError { None, Unauthenticated, Forbidden, NotFound, Conflict, Invalid }
public sealed record SupplierInvoiceResult<T>(T? Value, SupplierInvoiceError Error, string? Message)
{
    public bool IsSuccess => Error == SupplierInvoiceError.None;
    public static SupplierInvoiceResult<T> Success(T value) => new(value, SupplierInvoiceError.None, null);
    public static SupplierInvoiceResult<T> Failure(SupplierInvoiceError error, string message) => new(default, error, message);
}
