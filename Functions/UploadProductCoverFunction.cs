using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using Platform.ProductCoverUpload.Function.Configurations;
using Platform.ProductCoverUpload.Function.Helpers;
using Platform.ProductCoverUpload.Function.Services;
using System.Net;

namespace Platform.ProductCoverUpload.Function.Functions;

public sealed class UploadProductCoverFunction
{
    private readonly ProductCoverUploadService _productCoverUploadService;
    private readonly BlobStorageOptions _blobStorageOptions;

    public UploadProductCoverFunction(
        ProductCoverUploadService productCoverUploadService,
        IOptions<BlobStorageOptions> blobStorageOptions)
    {
        _productCoverUploadService = productCoverUploadService;
        _blobStorageOptions = blobStorageOptions.Value;
    }

    [Function(nameof(UploadProductCoverFunction))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "products/{productId:guid}/cover")]
        HttpRequestData request,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var (file, readError) = await MultipartFileReader.ReadSingleFileAsync(request, cancellationToken);
        if (readError is not null || file is null)
            return await HttpRequestHelpers.CreateBadRequestAsync(request, readError!, cancellationToken);

        if (file.FileSize == 0)
            return await HttpRequestHelpers.CreateBadRequestAsync(request, "File is empty.", cancellationToken);

        var maxFileSizeInBytes = (long)_blobStorageOptions.MaxFileSizeInMb * 1024 * 1024;
        if (file.FileSize > maxFileSizeInBytes)
            return await HttpRequestHelpers.CreateBadRequestAsync(
                request,
                $"File size must not exceed {_blobStorageOptions.MaxFileSizeInMb} MB.",
                cancellationToken);

        try
        {
            var result = await _productCoverUploadService.UploadAsync(productId, file, cancellationToken);
            var okResponse = request.CreateResponse(HttpStatusCode.OK);
            await okResponse.WriteAsJsonAsync(result, cancellationToken);
            return okResponse;
        }
        catch (InvalidOperationException ex)
        {
            return await HttpRequestHelpers.CreateBadRequestAsync(request, ex.Message, cancellationToken);
        }
        catch (Exception)
        {
            var internalError = request.CreateResponse(HttpStatusCode.InternalServerError);
            await internalError.WriteStringAsync("Unable to upload product cover image.", cancellationToken);
            return internalError;
        }
    }
}
