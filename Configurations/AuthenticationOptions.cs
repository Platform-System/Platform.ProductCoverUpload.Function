namespace Platform.ProductCoverUpload.Function.Configurations;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}
