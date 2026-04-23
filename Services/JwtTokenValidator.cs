using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Platform.ProductCoverUpload.Function.Configurations;
using System.IdentityModel.Tokens.Jwt;

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

    public async Task<bool> IsValidAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Authority) || string.IsNullOrWhiteSpace(_options.Audience))
            return false;

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
            new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
