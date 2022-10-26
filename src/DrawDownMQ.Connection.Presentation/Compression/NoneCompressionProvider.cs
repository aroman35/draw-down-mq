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
}