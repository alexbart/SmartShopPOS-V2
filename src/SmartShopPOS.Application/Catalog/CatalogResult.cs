namespace SmartShopPOS.Application.Catalog;

public enum CatalogError
{
    None,
    Unauthenticated,
    Forbidden,
    NotFound,
    Conflict,
    Invalid
}

public sealed record CatalogResult<T>(T? Value, CatalogError Error, string? Message)
{
    public bool IsSuccess => Error == CatalogError.None;

    public static CatalogResult<T> Success(T value) => new(value, CatalogError.None, null);

    public static CatalogResult<T> Failure(CatalogError error, string message) => new(default, error, message);
}