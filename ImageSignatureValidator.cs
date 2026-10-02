namespace OneRugbyNavi2;

public static class ImageSignatureValidator
{
    public static string? GetExtension(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return ".png";
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return ".jpg";
        if (data.Length >= 6 &&
            (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8))) return ".gif";
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8)) return ".webp";
        return null;
    }
}
