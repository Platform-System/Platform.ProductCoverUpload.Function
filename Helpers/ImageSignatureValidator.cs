using Platform.ProductCoverUpload.Function.Models;

namespace Platform.ProductCoverUpload.Function.Helpers;

public static class ImageSignatureValidator
{
    public static bool IsValid(MultipartFileData file)
    {
        // Content-Type là thông tin client gửi lên nên có thể bị giả mạo.
        // Vì vậy mình đọc vài byte đầu để kiểm tra chữ ký thật của JPEG/PNG/WEBP.
        Span<byte> header = stackalloc byte[12];
        var originalPosition = file.FileStream.CanSeek ? file.FileStream.Position : 0;
        var bytesRead = file.FileStream.Read(header);

        if (file.FileStream.CanSeek)
            file.FileStream.Position = originalPosition;

        return file.ContentType switch
        {
            "image/jpeg" => IsJpeg(header, bytesRead),
            "image/png" => IsPng(header, bytesRead),
            "image/webp" => IsWebp(header, bytesRead),
            _ => false
        };
    }

    private static bool IsJpeg(ReadOnlySpan<byte> header, int bytesRead)
        => bytesRead >= 3 &&
           header[0] == 0xFF &&
           header[1] == 0xD8 &&
           header[2] == 0xFF;

    private static bool IsPng(ReadOnlySpan<byte> header, int bytesRead)
        => bytesRead >= 8 &&
           header[0] == 0x89 &&
           header[1] == 0x50 &&
           header[2] == 0x4E &&
           header[3] == 0x47 &&
           header[4] == 0x0D &&
           header[5] == 0x0A &&
           header[6] == 0x1A &&
           header[7] == 0x0A;

    private static bool IsWebp(ReadOnlySpan<byte> header, int bytesRead)
        => bytesRead >= 12 &&
           header[0] == 0x52 &&
           header[1] == 0x49 &&
           header[2] == 0x46 &&
           header[3] == 0x46 &&
           header[8] == 0x57 &&
           header[9] == 0x45 &&
           header[10] == 0x42 &&
           header[11] == 0x50;
}
