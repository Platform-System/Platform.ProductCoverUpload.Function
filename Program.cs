using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        services.AddSingleton<ProductCoverUploadService>();
        services.AddSingleton<JwtTokenValidator>();
    })
    .Build();

await host.RunAsync();
