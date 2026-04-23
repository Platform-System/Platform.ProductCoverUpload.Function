using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Platform.ProductCoverUpload.Function.Configurations;
using Platform.ProductCoverUpload.Function.Helpers;
using Platform.ProductCoverUpload.Function.Models;
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
        // Function này chỉ chấp nhận request multipart/form-data
        // vì cover image được gửi dưới dạng file upload từ client.
        if (!request.Headers.TryGetValues("Content-Type", out var contentTypes))
            return await HttpRequestHelpers.CreateBadRequestAsync(request, "Request must be multipart/form-data.", cancellationToken);

        var contentType = contentTypes.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(contentType) ||
            !contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return await HttpRequestHelpers.CreateBadRequestAsync(request, "Request must be multipart/form-data.", cancellationToken);

        // Boundary là chuỗi phân cách giữa các phần dữ liệu trong multipart/form-data.
        // Có thể hiểu đơn giản: request upload file sẽ không gửi body thành 1 cục duy nhất,
        // mà chia thành nhiều phần (section), ví dụ phần file, phần text, phần metadata...
        // Boundary chính là "vạch ngăn" để hệ thống biết phần nào bắt đầu, phần nào kết thúc.
        // Không có boundary thì không thể tách body ra để đọc file.
        var boundary = HttpRequestHelpers.ExtractMultipartBoundary(contentType);
        if (string.IsNullOrWhiteSpace(boundary))
            return await HttpRequestHelpers.CreateBadRequestAsync(request, "Missing multipart boundary.", cancellationToken);

        // MultipartReader là lớp dùng để đọc request body theo từng section dựa vào boundary ở trên.
        // Nói ngắn gọn: nó giúp mình duyệt lần lượt từng phần dữ liệu bên trong request upload.
        var reader = new MultipartReader(boundary, request.Body);
        // Mỗi section là 1 phần dữ liệu nhỏ trong multipart body.
        // Với case này mình chỉ quan tâm section nào thực sự là file ảnh.
        MultipartSection? section;
        MultipartFileData? file = null;
        var fileCount = 0;

        // Đọc lần lượt từng section trong request body cho tới khi hết dữ liệu.
        while ((section = await reader.ReadNextSectionAsync(cancellationToken)) is not null)
        {
            // Content-Disposition chứa metadata của section hiện tại,
            // ví dụ đây là field thường hay là file upload, tên field là gì, tên file là gì.
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition))
                continue;

            // Chỉ lấy section thật sự là file upload.
            // Các field form-data khác nếu có sẽ bị bỏ qua.
            if (disposition?.DispositionType != "form-data" || string.IsNullOrWhiteSpace(disposition.FileName.Value))
                continue;

            fileCount++;
            // Cover của product chỉ cho phép đúng 1 ảnh.
            if (fileCount > 1)
                return await HttpRequestHelpers.CreateBadRequestAsync(request, "Only one cover image is allowed.", cancellationToken);

            var fileStream = new MemoryStream();
            await section.Body.CopyToAsync(fileStream, cancellationToken);
            fileStream.Position = 0;

            // Gom dữ liệu file về 1 model trung gian để service
            // chỉ tập trung vào upload blob, không cần biết HTTP multipart hoạt động ra sao.
            file = new MultipartFileData
            {
                FileName = disposition.FileName.Value ?? disposition.FileNameStar.Value ?? string.Empty,
                ContentType = section.ContentType ?? "application/octet-stream",
                FileSize = fileStream.Length,
                FileStream = fileStream
            };
        }

        // Sau khi đọc xong toàn bộ multipart body mà vẫn không có file
        // thì coi như request không hợp lệ.
        if (file is null || string.IsNullOrWhiteSpace(file.FileName))
            return await HttpRequestHelpers.CreateBadRequestAsync(request, "File is required.", cancellationToken);

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
            // Từ đây function bàn giao việc upload thật sự cho service xử lý.
            var result = await _productCoverUploadService.UploadAsync(productId, file, cancellationToken);
            var okResponse = request.CreateResponse(HttpStatusCode.OK);
            await okResponse.WriteAsJsonAsync(result, cancellationToken);
            return okResponse;
        }
        catch (InvalidOperationException ex)
        {
            // Các lỗi validate/config do service chủ động trả về sẽ map thành 400.
            return await HttpRequestHelpers.CreateBadRequestAsync(request, ex.Message, cancellationToken);
        }
        catch (Exception)
        {
            // Lỗi không mong muốn của hạ tầng upload blob sẽ gom về 500.
            var internalError = request.CreateResponse(HttpStatusCode.InternalServerError);
            await internalError.WriteStringAsync("Unable to upload product cover image.", cancellationToken);
            return internalError;
        }
    }
}
