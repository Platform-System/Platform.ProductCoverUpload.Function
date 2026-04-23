using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Net.Http.Headers;
using System.Net;

namespace Platform.ProductCoverUpload.Function.Helpers;

public static class HttpRequestHelpers
{
    public static string? ExtractMultipartBoundary(string contentType)
    {
        // Header Content-Type của multipart/form-data thường có dạng:
        // multipart/form-data; boundary=----WebKitFormBoundaryabc123
        // Hàm này lấy riêng phần boundary ra để code phía trên dùng nó tách request body.
        if (!MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
            return null;

        return HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
    }

    public static async Task<HttpResponseData> CreateBadRequestAsync(
        HttpRequestData request,
        string message,
        CancellationToken cancellationToken)
    {
        // Gom logic tạo response 400 vào một chỗ để function chính đỡ lặp code.
        var response = request.CreateResponse(HttpStatusCode.BadRequest);
        await response.WriteStringAsync(message, cancellationToken);
        return response;
    }
}
