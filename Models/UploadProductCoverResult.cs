namespace Platform.ProductCoverUpload.Function.Models;

public sealed class UploadProductCoverResult
{
    // Kết quả trả về sau khi upload thành công.
    // Chỉ giữ metadata của file, không trả url public/private tại đây.
    public string FileName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
}
