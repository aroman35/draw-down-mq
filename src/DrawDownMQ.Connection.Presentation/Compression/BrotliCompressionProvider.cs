using System.IO.Compression;
using DrawDownMQ.Connection.Abstractions;

namespace DrawDownMQ.Connection.Presentation.Compression;

public class BrotliCompressionProvider : ICompressionProvider
{
    public void Decompress(ReadOnlySpan<byte> sourceMessage, Span<byte> buffer)
    {
        using var brotli = new BrotliDecoder();
        brotli.Decompress(sourceMessage, buffer, out _, out _);
    }

    public int Compress(ReadOnlySpan<byte> sourceMessage, Span<byte> buffer)
    {
        using var brotli = new BrotliEncoder();
        brotli.Compress(sourceMessage, buffer, out _, out var bytesWritten, true);
        return bytesWritten;
    }
}