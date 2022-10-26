using DrawDownMQ.Connection.Common;

namespace DrawDownMQ.Connection.Abstractions;

public interface ICompressionSwitcher
{
    ICompressionProvider Create(CompressionType compressionType);
}