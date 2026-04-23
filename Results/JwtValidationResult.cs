namespace Platform.ProductCoverUpload.Function.Results;

public sealed class JwtValidationResult
{
    public bool IsValid { get; init; }
    public Guid? UserId { get; init; }
    public IReadOnlyCollection<string> Roles { get; init; } = [];

    public static JwtValidationResult Valid(Guid userId, IReadOnlyCollection<string> roles)
        => new()
        {
            IsValid = true,
            UserId = userId,
            Roles = roles
        };

    public static JwtValidationResult Invalid()
        => new();
}
