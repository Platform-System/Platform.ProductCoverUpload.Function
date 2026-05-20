using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using Platform.ProductCoverUpload.Function.Configurations;
using Platform.ProductCoverUpload.Function.Enums;
using Platform.ProductCoverUpload.Function.Helpers;
using Platform.ProductCoverUpload.Function.Services;
using System.Net;

namespace Platform.ProductCoverUpload.Function.Functions;

public sealed class UploadProductCoverFunction
{
    private readonly ProductCoverUploadService _productCoverUploadService;
    private readonly BlobStorageOptions _blobStorageOptions;
    private readonly JwtTokenValidator _jwtTokenValidator;
    private readonly CatalogAuthorizationClient _catalogAuthorizationClient;

    public UploadProductCoverFunction(
        ProductCoverUploadService productCoverUploadService,
        IOptions<BlobStorageOptions> blobStorageOptions,
        JwtTokenValidator jwtTokenValidator,
        CatalogAuthorizationClient catalogAuthorizationClient)
    {
        _productCoverUploadService = productCoverUploadService;
        _blobStorageOptions = blobStorageOptions.Value;
        _jwtTokenValidator = jwtTokenValidator;
        _catalogAuthorizationClient = catalogAuthorizationClient;
    }

    /// <summary>
    /// Uploads a product cover image.
    /// </summary>
    [Function(nameof(UploadProductCoverFunction))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "products/{productId:guid}/cover")]
        HttpRequestData request,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var token = request.GetBearerToken();
        if (token is null)
        {
            var unauthorized = request.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorized.WriteStringAsync("Unauthorized.", cancellationToken);
            return unauthorized;
        }

        var validation = await _jwtTokenValidator.ValidateAsync(token, cancellationToken);
        if (!validation.IsValid || validation.UserId is null)
        {
            var unauthorized = request.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorized.WriteStringAsync("Unauthorized.", cancellationToken);
            return unauthorized;
        }

        var authorization = await _catalogAuthorizationClient.AuthorizeProductCoverUploadAsync(
            productId,
            validation.UserId.Value,
            validation.Roles,
            cancellationToken);

        if (!authorization.IsAllowed)
        {
            var statusCode = authorization.Status == ProductCoverUploadAuthorizationStatus.Unavailable
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.Forbidden;

            var denied = request.CreateResponse(statusCode);
            await denied.WriteStringAsync(authorization.Error ?? "Forbidden.", cancellationToken);
            return denied;
        }

        // Bước 1: đọc đúng 1 file ảnh từ request multipart/form-data.
        // Chi tiết multipart khó đọc nên được gom vào MultipartFileReader.
        var (file, readError) = await MultipartFileReader.ReadSingleFileAsync(request, cancellationToken);
        if (readError is not null || file is null)
            return await HttpRequestHelpers.CreateBadRequestAsync(request, readError!, cancellationToken);

        // Bước 2: validate dung lượng file trước khi upload lên Azure Blob.
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
            // Bước 3: upload file lên Blob Storage và trả metadata cho client.
            var result = await _productCoverUploadService.UploadAsync(
                productId,
                file,
                authorization.Visibility!.Value,
                cancellationToken);

            var setCoverResult = await _catalogAuthorizationClient.SetProductCoverAsync(
                productId,
                validation.UserId.Value,
                result,
                cancellationToken);

            if (!setCoverResult.IsSuccess)
            {
                await _productCoverUploadService.DeleteAsync(result, cancellationToken);

                var statusCode = setCoverResult.IsUnavailable
                    ? HttpStatusCode.ServiceUnavailable
                    : HttpStatusCode.BadRequest;

                var failed = request.CreateResponse(statusCode);
                await failed.WriteStringAsync(setCoverResult.Error ?? "Unable to save product cover.", cancellationToken);
                return failed;
            }

            var okResponse = request.CreateResponse(HttpStatusCode.OK);
            await okResponse.WriteAsJsonAsync(result, cancellationToken);
            return okResponse;
        }
        catch (InvalidOperationException ex)
        {
            // Lỗi do validate/config sai thì trả 400 để client biết request chưa hợp lệ.
            return await HttpRequestHelpers.CreateBadRequestAsync(request, ex.Message, cancellationToken);
        }
        catch (Azure.RequestFailedException)
        {
            var internalError = request.CreateResponse(HttpStatusCode.InternalServerError);
            await internalError.WriteStringAsync("Unable to upload product cover image.", cancellationToken);
            return internalError;
        }
        catch (Exception)
        {
            // Lỗi ngoài dự kiến, ví dụ Blob Storage lỗi, thì trả 500.
            var internalError = request.CreateResponse(HttpStatusCode.InternalServerError);
            await internalError.WriteStringAsync("Unable to upload product cover image.", cancellationToken);
            return internalError;
        }
    }
}
