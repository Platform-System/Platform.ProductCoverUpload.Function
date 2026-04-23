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
        if (!request.Headers.TryGetValues("Content-Type", out var contentTypes))
            return (null, "Request must be multipart/form-data.");

        var contentType = contentTypes.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(contentType) ||
            !contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return (null, "Request must be multipart/form-data.");

        // Boundary la chuoi ngan cach cac phan trong multipart body.
        var boundary = HttpRequestHelpers.ExtractMultipartBoundary(contentType);
        if (string.IsNullOrWhiteSpace(boundary))
            return (null, "Missing multipart boundary.");

        // MultipartReader doc tung section trong body, minh chi lay section la file.
        var reader = new MultipartReader(boundary, request.Body);
        MultipartFileData? file = null;
        var fileCount = 0;

        MultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync(cancellationToken)) is not null)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition))
                continue;

            if (disposition?.DispositionType != "form-data" || string.IsNullOrWhiteSpace(disposition.FileName.Value))
                continue;

            fileCount++;
            if (fileCount > 1)
                return (null, "Only one cover image is allowed.");

            var fileStream = new MemoryStream();
            await section.Body.CopyToAsync(fileStream, cancellationToken);
            fileStream.Position = 0;

            file = new MultipartFileData
            {
                FileName = disposition.FileName.Value ?? disposition.FileNameStar.Value ?? string.Empty,
                ContentType = section.ContentType ?? "application/octet-stream",
                FileSize = fileStream.Length,
                FileStream = fileStream
            };
        }

        if (file is null || string.IsNullOrWhiteSpace(file.FileName))
            return (null, "File is required.");

        return (file, null);
    }
}
