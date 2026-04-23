using Platform.ProductCoverUpload.Function.Enums;

namespace Platform.ProductCoverUpload.Function.Results;

public sealed class ProductCoverUploadAuthorizationResult
{
    public bool IsAllowed { get; init; }
    public ProductCoverUploadAuthorizationStatus Status { get; init; }
    public ProductCoverUploadVisibility? Visibility { get; init; }
    public string? Error { get; init; }

    public static ProductCoverUploadAuthorizationResult Allowed(ProductCoverUploadVisibility visibility)
        => new()
        {
            IsAllowed = true,
            Status = ProductCoverUploadAuthorizationStatus.Allowed,
            Visibility = visibility
        };

    public static ProductCoverUploadAuthorizationResult Denied(string error)
        => new()
        {
            IsAllowed = false,
            Status = ProductCoverUploadAuthorizationStatus.Denied,
            Error = error
        };

    public static ProductCoverUploadAuthorizationResult Unavailable(string error)
        => new()
        {
            IsAllowed = false,
            Status = ProductCoverUploadAuthorizationStatus.Unavailable,
            Error = error
        };
}
