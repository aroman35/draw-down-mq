using DrawDownMQ.Connection.Abstractions;

namespace DrawDownMQ.Connection.Presentation.Compression;

public class NoneCompressionProvider : ICompressionProvider
{
    public Task<byte[]> Compress(byte[] sourceMessage, CancellationToken cancellationToken)
    {
        return Task.FromResult(sourceMessage);
    }

    public Task<byte[]> Decompress(byte[] compressedMessage, CancellationToken cancellationToken)
    {
        return Task.FromResult(compressedMessage);
    }

    public async Task Decompress(Memory<byte> sourceMessage, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public void Decompress(ReadOnlySpan<byte> compressed, Span<byte> buffer)
    {
        throw new NotImplementedException();
    }
}