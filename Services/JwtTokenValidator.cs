using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Platform.ProductCoverUpload.Function.Configurations;
using Platform.ProductCoverUpload.Function.Results;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Platform.ProductCoverUpload.Function.Services;

public sealed class JwtTokenValidator
{
    private readonly AuthenticationOptions _options;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;

    public JwtTokenValidator(IOptions<AuthenticationOptions> options)
    {
        _options = options.Value;
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{_options.Authority.TrimEnd('/')}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever());
    }

    public async Task<JwtValidationResult> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Authority) || string.IsNullOrWhiteSpace(_options.Audience))
            return JwtValidationResult.Invalid();

        var configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Authority.TrimEnd('/'),
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);

            var userIdValue = FindFirstValue(principal, ClaimTypes.NameIdentifier)
                ?? FindFirstValue(principal, "sub");

            if (!Guid.TryParse(userIdValue, out var userId))
                return JwtValidationResult.Invalid();

            var roles = KeycloakRoleReader.ReadRoles(principal).ToArray();
            return JwtValidationResult.Valid(userId, roles);
        }
        catch
        {
            return JwtValidationResult.Invalid();
        }
    }

    private static string? FindFirstValue(ClaimsPrincipal principal, string claimType)
        => principal.FindFirst(claimType)?.Value;
}
