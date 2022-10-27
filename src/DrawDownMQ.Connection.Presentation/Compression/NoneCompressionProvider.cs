using DrawDownMQ.Connection.Abstractions;

namespace DrawDownMQ.Connection.Presentation.Compression;

public class NoneCompressionProvider : ICompressionProvider
{
    public void Decompress(ReadOnlySpan<byte> sourceMessage, Span<byte> buffer)
    {
        sourceMessage.CopyTo(buffer);
    }

    public int Compress(ReadOnlySpan<byte> sourceMessage, Span<byte> buffer)
    {
        sourceMessage.CopyTo(buffer);
        return sourceMessage.Length;
    }
}