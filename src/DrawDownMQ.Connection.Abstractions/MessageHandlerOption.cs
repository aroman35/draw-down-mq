using DrawDownMQ.Connection.Common;

namespace DrawDownMQ.Connection.Abstractions;

public class MessageHandlerOption
{
    public HashType HashType { get; set; }
    public CompressionType CompressionType { get; set; }
}