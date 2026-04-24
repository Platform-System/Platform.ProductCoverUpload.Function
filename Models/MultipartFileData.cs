namespace Platform.ProductCoverUpload.Function.Models;

public sealed class MultipartFileData
{
    // Dữ liệu file đã đọc ra từ HTTP multipart request,
    // dùng để chuyển từ function sang service upload.
    public Stream FileStream { get; set; } = Stream.Null;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string AltText { get; set; } = string.Empty;
}
