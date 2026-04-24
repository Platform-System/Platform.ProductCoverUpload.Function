using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Platform.ProductCoverUpload.Function.Configurations;
using Platform.ProductCoverUpload.Function.Constants;
using Platform.ProductCoverUpload.Function.Enums;
using Platform.ProductCoverUpload.Function.Helpers;
using Platform.ProductCoverUpload.Function.Models;

namespace Platform.ProductCoverUpload.Function.Services;

public sealed class ProductCoverUploadService
{
    // Chỉ cho phép upload các định dạng ảnh dùng cho cover.
    private static readonly HashSet<string> AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    private readonly BlobStorageOptions _blobStorageOptions;

    public ProductCoverUploadService(IOptions<BlobStorageOptions> blobStorageOptions)
    {
        _blobStorageOptions = blobStorageOptions.Value;
    }

    public async Task<UploadProductCoverResult> UploadAsync(
        Guid productId,
        MultipartFileData file,
        ProductCoverUploadVisibility visibility,
        CancellationToken cancellationToken)
    {
        // Validate loại file trước khi đụng tới blob storage.
        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new InvalidOperationException("Only JPEG, PNG, and WEBP images are allowed.");

        if (!ImageSignatureValidator.IsValid(file))
            throw new InvalidOperationException("File content does not match a supported image format.");

        // Hai config này là tối thiểu để service biết upload vào đâu.
        if (string.IsNullOrWhiteSpace(_blobStorageOptions.ConnectionString))
            throw new InvalidOperationException("Blob storage connection string is not configured.");

        // Giữ lại extension gốc để file upload sau này vẫn đúng định dạng.
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
            throw new InvalidOperationException("File extension is required.");

        // Sinh tên file mới để tránh trùng tên khi nhiều client upload cùng lúc.
        var generatedFileName = $"{Guid.NewGuid():N}{extension}";
        var blobName = $"products/{productId}/cover/{generatedFileName}";

        var blobServiceClient = new BlobServiceClient(_blobStorageOptions.ConnectionString);
        var containerName = visibility == ProductCoverUploadVisibility.Public
            ? BlobContainerNames.ProductsPublic
            : BlobContainerNames.ProductsPrivate;
        var containerAccessType = visibility == ProductCoverUploadVisibility.Public
            ? PublicAccessType.Blob
            : PublicAccessType.None;
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(containerAccessType, cancellationToken: cancellationToken);
        await containerClient.SetAccessPolicyAsync(containerAccessType, cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobName);

        // Upload file lên đúng path cover của product.
        await using (file.FileStream)
        {
            await blobClient.UploadAsync(
                file.FileStream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = file.ContentType
                    }
                },
                cancellationToken);
        }

        // Chỉ trả metadata của file đã upload để Catalog lưu metadata vào DB.
        return new UploadProductCoverResult
        {
            FileName = generatedFileName,
            BlobName = blobName,
            ContainerName = containerClient.Name,
            ContentType = file.ContentType,
            Size = file.FileSize,
            AltText = file.AltText
        };
    }
}
