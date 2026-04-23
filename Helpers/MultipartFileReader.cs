using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Net.Http.Headers;
using Platform.ProductCoverUpload.Function.Models;

namespace Platform.ProductCoverUpload.Function.Helpers;

public static class MultipartFileReader
{
    public static async Task<(MultipartFileData? File, string? Error)> ReadSingleFileAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        // Request upload file phải là multipart/form-data.
        // Nếu không đúng format này thì sẽ không đọc được file từ body.
        if (!request.Headers.TryGetValues("Content-Type", out var contentTypes))
            return (null, "Request must be multipart/form-data.");

        var contentType = contentTypes.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(contentType) ||
            !contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return (null, "Request must be multipart/form-data.");

        // Boundary là chuỗi ngăn cách các phần trong multipart body.
        // Có boundary thì MultipartReader mới biết tách body ra thành từng phần nhỏ.
        var boundary = HttpRequestHelpers.ExtractMultipartBoundary(contentType);
        if (string.IsNullOrWhiteSpace(boundary))
            return (null, "Missing multipart boundary.");

        // MultipartReader đọc từng section trong body.
        // Với upload cover, mình chỉ lấy section nào là file.
        var reader = new MultipartReader(boundary, request.Body);
        MultipartFileData? file = null;
        var fileCount = 0;

        MultipartSection? section;
        // Duyệt từng phần trong request body cho tới khi hết dữ liệu.
        while ((section = await reader.ReadNextSectionAsync(cancellationToken)) is not null)
        {
            // Content-Disposition cho biết section này có phải file hay chỉ là field text.
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition))
                continue;

            // Bỏ qua các section không phải file upload.
            if (disposition?.DispositionType != "form-data" || string.IsNullOrWhiteSpace(disposition.FileName.Value))
                continue;

            fileCount++;
            if (fileCount > 1) // Cover chỉ được upload 1 ảnh, nếu gửi nhiều file thì từ chối.
                return (null, "Only one cover image is allowed.");

            // Copy file stream ra MemoryStream để service phía sau upload lên Blob Storage.
            var fileStream = new MemoryStream();
            await section.Body.CopyToAsync(fileStream, cancellationToken);
            fileStream.Position = 0;

            file = new MultipartFileData // Map thông tin file sang model đơn giản để function/service dễ dùng.
            {
                FileName = disposition.FileName.Value ?? disposition.FileNameStar.Value ?? string.Empty,
                ContentType = section.ContentType ?? "application/octet-stream",
                FileSize = fileStream.Length,
                FileStream = fileStream
            };
        }

        // Không tìm được file nào trong request.
        if (file is null || string.IsNullOrWhiteSpace(file.FileName))
            return (null, "File is required.");

        return (file, null);
    }
}
