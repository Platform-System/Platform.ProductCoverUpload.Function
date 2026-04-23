using Microsoft.Azure.Functions.Worker.Http;

namespace Platform.ProductCoverUpload.Function.Helpers;

public static class AuthorizationHeaderHelpers
{
    public static string? GetBearerToken(this HttpRequestData request)
    {
        if (!request.Headers.TryGetValues("Authorization", out var values))
            return null;

        var authorization = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authorization))
            return null;

        const string bearerPrefix = "Bearer ";
        return authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[bearerPrefix.Length..].Trim()
            : null;
    }
}
