namespace SmartShopPOS.Application.UserBranches;

public enum UserBranchError
{
    None,
    Unauthenticated,
    Forbidden,
    NotFound,
    Conflict,
    Invalid
}

public sealed record UserBranchResult<T>(T? Value, UserBranchError Error, string? Message)
{
    public bool IsSuccess => Error == UserBranchError.None;

    public static UserBranchResult<T> Success(T value) => new(value, UserBranchError.None, null);

    public static UserBranchResult<T> Failure(UserBranchError error, string message) => new(default, error, message);
}