using Microsoft.Extensions.Options;
using Platform.ProductCoverUpload.Function.Configurations;
using Platform.ProductCoverUpload.Function.Enums;
using Platform.ProductCoverUpload.Function.Models;
using Platform.ProductCoverUpload.Function.Services;
using Xunit;

namespace Platform.ProductCoverUpload.Function.Tests.Services;

public sealed class ProductCoverUploadServiceTests
{
    [Fact]
    public async Task UploadAsync_WhenContentTypeIsUnsupported_ThrowsInvalidOperation()
    {
        var service = CreateService(connectionString: "UseDevelopmentStorage=true");
        var file = CreateFile("cover.gif", "image/gif", [0x47, 0x49, 0x46]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadAsync(Guid.NewGuid(), file, ProductCoverUploadVisibility.Public, CancellationToken.None));

        Assert.Equal("Only JPEG, PNG, and WEBP images are allowed.", exception.Message);
    }

    [Fact]
    public async Task UploadAsync_WhenImageSignatureDoesNotMatch_ThrowsInvalidOperation()
    {
        var service = CreateService(connectionString: "UseDevelopmentStorage=true");
        var file = CreateFile("cover.png", "image/png", [0x01, 0x02, 0x03, 0x04]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadAsync(Guid.NewGuid(), file, ProductCoverUploadVisibility.Public, CancellationToken.None));

        Assert.Equal("File content does not match a supported image format.", exception.Message);
    }

    [Fact]
    public async Task UploadAsync_WhenConnectionStringMissing_ThrowsInvalidOperation()
    {
        var service = CreateService(connectionString: "");
        var file = CreateFile("cover.png", "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadAsync(Guid.NewGuid(), file, ProductCoverUploadVisibility.Private, CancellationToken.None));

        Assert.Equal("Blob storage connection string is not configured.", exception.Message);
    }

    [Fact]
    public async Task UploadAsync_WhenFileHasNoExtension_ThrowsInvalidOperation()
    {
        var service = CreateService(connectionString: "UseDevelopmentStorage=true");
        var file = CreateFile("cover", "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadAsync(Guid.NewGuid(), file, ProductCoverUploadVisibility.Public, CancellationToken.None));

        Assert.Equal("File extension is required.", exception.Message);
    }

    private static ProductCoverUploadService CreateService(string connectionString)
        => new(Options.Create(new BlobStorageOptions
        {
            ConnectionString = connectionString
        }));

    private static MultipartFileData CreateFile(string fileName, string contentType, byte[] bytes)
        => new()
        {
            FileName = fileName,
            ContentType = contentType,
            FileSize = bytes.Length,
            FileStream = new MemoryStream(bytes),
            AltText = "cover"
        };
}
