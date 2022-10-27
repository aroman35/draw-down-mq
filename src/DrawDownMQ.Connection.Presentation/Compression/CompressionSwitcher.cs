using DrawDownMQ.Connection.Abstractions;
using DrawDownMQ.Connection.Common;

namespace DrawDownMQ.Connection.Presentation.Compression;

public class CompressionSwitcher : ICompressionSwitcher
{
    public ICompressionProvider Create(CompressionType compressionType)
    {
        return compressionType switch
        {
            CompressionType.Brotli => new BrotliCompressionProvider(),
            CompressionType.None => new NoneCompressionProvider(),
            _ => new NoneCompressionProvider()
        };
    }
}