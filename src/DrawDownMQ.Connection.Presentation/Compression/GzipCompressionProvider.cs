using System.IO.Compression;
using DrawDownMQ.Connection.Abstractions;

namespace DrawDownMQ.Connection.Presentation.Compression;

public class GzipCompressionProvider : ICompressionProvider
{
    public async Task<byte[]> Compress(byte[] sourceMessage, CancellationToken cancellationToken)
    {
        using var compressedMessage = new MemoryStream();
        await using var gzipStream = new GZipStream(compressedMessage, CompressionMode.Compress);
        await gzipStream.WriteAsync(sourceMessage, cancellationToken);
        gzipStream.Close();
        return compressedMessage.ToArray();
    }

    public async Task<byte[]> Decompress(byte[] compressedMessage, CancellationToken cancellationToken)
    {
        using var compressedStream = new MemoryStream(compressedMessage);
        await using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var resultStream = new MemoryStream();
        await zipStream.CopyToAsync(resultStream, cancellationToken);
        return resultStream.ToArray();
    }
}