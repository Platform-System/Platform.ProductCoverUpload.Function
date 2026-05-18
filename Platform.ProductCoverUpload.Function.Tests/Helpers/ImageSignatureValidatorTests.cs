using Platform.ProductCoverUpload.Function.Helpers;
using Platform.ProductCoverUpload.Function.Models;
using Xunit;

namespace Platform.ProductCoverUpload.Function.Tests.Helpers;

public sealed class ImageSignatureValidatorTests
{
    [Fact]
    public void IsValid_WhenPngHeaderMatches_ReturnsTrueAndPreservesStreamPosition()
    {
        using var stream = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x11]);
        stream.Position = 0;

        var file = new MultipartFileData
        {
            FileName = "cover.png",
            ContentType = "image/png",
            FileSize = stream.Length,
            FileStream = stream
        };

        var isValid = ImageSignatureValidator.IsValid(file);

        Assert.True(isValid);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void IsValid_WhenHeaderDoesNotMatch_ReturnsFalse()
    {
        using var stream = new MemoryStream([0x00, 0x01, 0x02, 0x03]);

        var file = new MultipartFileData
        {
            FileName = "cover.webp",
            ContentType = "image/webp",
            FileSize = stream.Length,
            FileStream = stream
        };

        Assert.False(ImageSignatureValidator.IsValid(file));
    }
}
