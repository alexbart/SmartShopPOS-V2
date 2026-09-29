using SmartShopPOS.Contracts.Suppliers;

namespace SmartShopPOS.Application.Suppliers;

public interface ISupplierService
{
    Task<SupplierResult<IReadOnlyList<SupplierResponse>>> ListAsync(SupplierFilter filter, CancellationToken cancellationToken = default);
    Task<SupplierResult<SupplierResponse>> GetAsync(Guid supplierId, CancellationToken cancellationToken = default);
    Task<SupplierResult<SupplierResponse>> CreateAsync(SupplierUpsertRequest request, CancellationToken cancellationToken = default);
    Task<SupplierResult<SupplierResponse>> UpdateAsync(Guid supplierId, SupplierUpsertRequest request, CancellationToken cancellationToken = default);
    Task<SupplierResult<bool>> DeactivateAsync(Guid supplierId, CancellationToken cancellationToken = default);
}

public enum SupplierError { None, Unauthenticated, Forbidden, NotFound, Conflict, Invalid }

public sealed record SupplierResult<T>(T? Value, SupplierError Error, string? Message)
{
    public bool IsSuccess => Error == SupplierError.None;
    public static SupplierResult<T> Success(T value) => new(value, default, null);
    public static SupplierResult<T> Failure(SupplierError error, string message) => new(default, error, message);
}
