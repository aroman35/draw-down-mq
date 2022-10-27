using System.Text;

namespace DrawDownMQ.Connection.Abstractions;

public readonly ref struct DrawDownMqTransportMessage
{
    public static DrawDownMqTransportMessage Create(Guid key, ReadOnlySpan<byte> hash, ReadOnlySpan<byte> message)
    {
        var instance = new DrawDownMqTransportMessage
        {
            Key = key,
            Hash = hash,
            Message = message
        };

        return instance;
    }
    
    public Guid Key { get; init; }
    public ReadOnlySpan<byte> Hash { get; init; }
    public ReadOnlySpan<byte> Message { get; init; }

    public override string ToString()
    {
        return Encoding.UTF8.GetString(Message);
    }
}