using System.IO.Compression;
using System.Text;

namespace Shared.Core.Helpers;

public static class CompressedExtension
{
    public static string Compress(string text)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(text);
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
        {
            gzipStream.Write(buffer, 0, buffer.Length);
        }
        return Convert.ToBase64String(memoryStream.ToArray());
    }

    public static string Decompress(string compressedText)
    {
        byte[] buffer = Convert.FromBase64String(compressedText);
        using var memoryStream = new MemoryStream(buffer);
        using var outputStream = new MemoryStream();
        using (var decompressStream = new GZipStream(memoryStream, CompressionMode.Decompress))
        {
            decompressStream.CopyTo(outputStream);
        }
        return Encoding.UTF8.GetString(outputStream.ToArray());
    }
}
