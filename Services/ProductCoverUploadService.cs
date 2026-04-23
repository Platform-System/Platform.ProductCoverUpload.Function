using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Platform.ProductCoverUpload.Function.Configurations;
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

    public async Task<UploadProductCoverResult> UploadAsync(Guid productId, MultipartFileData file, CancellationToken cancellationToken)
    {
        // Validate loại file trước khi đụng tới blob storage.
        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new InvalidOperationException("Only JPEG, PNG, and WEBP images are allowed.");

        // Hai config này là tối thiểu để service biết upload vào đâu.
        if (string.IsNullOrWhiteSpace(_blobStorageOptions.ConnectionString))
            throw new InvalidOperationException("Blob storage connection string is not configured.");

        if (string.IsNullOrWhiteSpace(_blobStorageOptions.ContainerName))
            throw new InvalidOperationException("Blob storage container name is not configured.");

        // Giữ lại extension gốc để file upload sau này vẫn đúng định dạng.
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
            throw new InvalidOperationException("File extension is required.");

        // Sinh tên file mới để tránh trùng tên khi nhiều client upload cùng lúc.
        var generatedFileName = $"{Guid.NewGuid():N}{extension}";
        var blobName = $"products/{productId}/cover/{generatedFileName}";

        var blobServiceClient = new BlobServiceClient(_blobStorageOptions.ConnectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_blobStorageOptions.ContainerName);
        // Nếu container chưa tồn tại thì tạo mới trước khi upload.
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobName);

        // Upload file lên đúng path cover của product.
        await using (file.FileStream)
        {
            await blobClient.UploadAsync(file.FileStream, overwrite: true, cancellationToken: cancellationToken);
        }

        // Chỉ trả metadata của file đã upload.
        // Việc public/private và generate url sẽ để service khác quyết định sau.
        return new UploadProductCoverResult
        {
            FileName = generatedFileName,
            BlobName = blobName,
            ContentType = file.ContentType,
            Size = file.FileSize
        };
    }
}
