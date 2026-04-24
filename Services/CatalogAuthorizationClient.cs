using Grpc.Core;
using Platform.Catalog.Grpc;
using Platform.ProductCoverUpload.Function.Enums;
using Platform.ProductCoverUpload.Function.Models;
using Platform.ProductCoverUpload.Function.Results;

namespace Platform.ProductCoverUpload.Function.Services;

public sealed class CatalogAuthorizationClient
{
    private readonly CatalogIntegration.CatalogIntegrationClient _client;

    public CatalogAuthorizationClient(CatalogIntegration.CatalogIntegrationClient client)
    {
        _client = client;
    }

    public async Task<ProductCoverUploadAuthorizationResult> AuthorizeProductCoverUploadAsync(
        Guid productId,
        Guid userId,
        IEnumerable<string> roles,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.AuthorizeProductCoverUploadAsync(
                new AuthorizeProductCoverUploadRequest
                {
                    ProductId = productId.ToString(),
                    UserId = userId.ToString(),
                    Roles = { roles }
                },
                cancellationToken: cancellationToken);

            if (!response.Status.IsSuccess)
                return ProductCoverUploadAuthorizationResult.Denied(response.Status.Errors.FirstOrDefault() ?? "Upload is not allowed.");

            return response.Data.Visibility switch
            {
                Platform.Catalog.Grpc.ProductCoverUploadVisibility.Public =>
                    ProductCoverUploadAuthorizationResult.Allowed(Enums.ProductCoverUploadVisibility.Public),
                Platform.Catalog.Grpc.ProductCoverUploadVisibility.Private =>
                    ProductCoverUploadAuthorizationResult.Allowed(Enums.ProductCoverUploadVisibility.Private),
                _ => ProductCoverUploadAuthorizationResult.Denied("Upload visibility is invalid.")
            };
        }
        catch (RpcException)
        {
            return ProductCoverUploadAuthorizationResult.Unavailable("Catalog service is unavailable.");
        }
    }

    public async Task<ProductCoverSetResult> SetProductCoverAsync(
        Guid productId,
        Guid userId,
        UploadProductCoverResult uploadResult,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.SetProductCoverAsync(
                new SetProductCoverRequest
                {
                    ProductId = productId.ToString(),
                    UserId = userId.ToString(),
                    BlobName = uploadResult.BlobName,
                    ContainerName = uploadResult.ContainerName,
                    FileName = uploadResult.FileName,
                    ContentType = uploadResult.ContentType,
                    Size = uploadResult.Size,
                    AltText = uploadResult.AltText
                },
                cancellationToken: cancellationToken);

            if (!response.Status.IsSuccess)
                return ProductCoverSetResult.Failure(response.Status.Errors.FirstOrDefault() ?? "Unable to save product cover.");

            return ProductCoverSetResult.Success();
        }
        catch (RpcException)
        {
            return ProductCoverSetResult.Unavailable("Catalog service is unavailable.");
        }
    }
}
