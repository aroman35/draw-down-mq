namespace DrawDownMQ.Connection.Abstractions;

public interface ICompressionProvider
{
    void Decompress(ReadOnlySpan<byte> sourceMessage, Span<byte> buffer);
    int Compress(ReadOnlySpan<byte> sourceMessage, Span<byte> buffer);
}