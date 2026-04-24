namespace Platform.ProductCoverUpload.Function.Results;

public sealed class ProductCoverSetResult
{
    public bool IsSuccess { get; private init; }
    public bool IsUnavailable { get; private init; }
    public string? Error { get; private init; }

    public static ProductCoverSetResult Success() => new() { IsSuccess = true };

    public static ProductCoverSetResult Failure(string error) => new() { Error = error };

    public static ProductCoverSetResult Unavailable(string error) => new() { IsUnavailable = true, Error = error };
}
