namespace SmartShopPOS.Application.Branches;

public enum BranchTerminalError
{
    None,
    Unauthenticated,
    Forbidden,
    NotFound,
    Conflict,
    Invalid
}

public sealed record BranchTerminalResult<T>(T? Value, BranchTerminalError Error, string? Message)
{
    public bool IsSuccess => Error == BranchTerminalError.None;

    public static BranchTerminalResult<T> Success(T value) => new(value, BranchTerminalError.None, null);

    public static BranchTerminalResult<T> Failure(BranchTerminalError error, string message) => new(default, error, message);
}