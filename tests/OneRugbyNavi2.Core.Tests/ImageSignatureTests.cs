using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class ImageSignatureTests
{
    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, ".png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, ".jpg")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, ".gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, ".webp")]
    public void Detects_extension_from_image_signature(byte[] data, string expected)
    {
        Assert.Equal(expected, ImageSignatureValidator.GetExtension(data));
    }

    [Fact]
    public void Rejects_html_even_if_it_is_named_as_a_png()
    {
        Assert.Null(ImageSignatureValidator.GetExtension("<html>unsupported image content</html>"u8));
    }
}
