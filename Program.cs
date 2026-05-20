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
            .Bind(context.Configuration.GetSection(BlobStorageOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "BlobStorage:ConnectionString is required.")
            .Validate(options => options.MaxFileSizeInMb > 0, "BlobStorage:MaxFileSizeInMb must be greater than 0.")
            .ValidateOnStart();

        services
            .AddOptions<AuthenticationOptions>()
            .Bind(context.Configuration.GetSection(AuthenticationOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Authority), "Authentication:Authority is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Authentication:Audience is required.")
            .ValidateOnStart();

        services
            .AddOptions<CatalogIntegrationOptions>()
            .Bind(context.Configuration.GetSection(CatalogIntegrationOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Address), "Integrations:Catalog:Address is required.")
            .ValidateOnStart();

        services.AddGrpcClient<CatalogIntegration.CatalogIntegrationClient>((serviceProvider, options) =>
        {
            var catalogOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CatalogIntegrationOptions>>().Value;
            options.Address = new Uri(catalogOptions.Address);
        });

        services.AddSingleton<ProductCoverUploadService>();
        services.AddSingleton<JwtTokenValidator>();
        services.AddScoped<CatalogAuthorizationClient>();
    })
    .Build();

await host.RunAsync();
