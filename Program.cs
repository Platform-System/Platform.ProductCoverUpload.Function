using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Catalog.Grpc;
using Platform.ProductCoverUpload.Function.Configurations;
using Platform.ProductCoverUpload.Function.Services;
using System.Text.Json;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        services
            .AddOptions<BlobStorageOptions>()
            .Bind(context.Configuration.GetSection(BlobStorageOptions.SectionName));

        services
            .AddOptions<AuthenticationOptions>()
            .Bind(context.Configuration.GetSection(AuthenticationOptions.SectionName));

        services
            .AddOptions<CatalogIntegrationOptions>()
            .Bind(context.Configuration.GetSection(CatalogIntegrationOptions.SectionName));

        var catalogAddress = context.Configuration[$"{CatalogIntegrationOptions.SectionName}:Address"];

        services.AddGrpcClient<CatalogIntegration.CatalogIntegrationClient>(options =>
        {
            options.Address = string.IsNullOrWhiteSpace(catalogAddress)
                ? new Uri("http://localhost")
                : new Uri(catalogAddress);
        });

        services.AddSingleton<ProductCoverUploadService>();
        services.AddSingleton<JwtTokenValidator>();
        services.AddScoped<CatalogAuthorizationClient>();
    })
    .Build();

await host.RunAsync();
