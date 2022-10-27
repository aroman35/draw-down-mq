using System.Text;

namespace DrawDownMQ.Connection.Common;

public struct SessionHeader
{
    public const string MaxLengthHeader = "MAX-LENGTH";
    public const string CompressionHeader = "COMPRESSION";
    public const string HashingTypeHeader = "HASHING-TYPE";
    public const string ClientIdHeader = "CLIENT-ID";
    public const string ClientNameHeader = "CLIENT-NAME";
    public const string ExchangeTypeHeader = "EXCHANGE-TYPE";
    public const string RingNameHeader = "RING-NAME";

    public SessionHeader(string rawHeader)
    {
        var nameValue = rawHeader.Split(':');
        Key = nameValue[0];
        Value = nameValue[1];
    }
    
    public SessionHeader(string key, string value)
    {
        Key = key;
        Value = value;
    }
    public string Key { get; set; }
    public string Value { get; set; }
    public override string ToString()
    {
        return $"{Key}:{Value}";
    }

    public static SessionHeader MaxLength(int maxMessageLength) => new(MaxLengthHeader, maxMessageLength.ToString());
    public static SessionHeader Compression(CompressionType compression) => new(CompressionHeader, compression.ToString());
    public static SessionHeader Hash(HashType hashType) => new(HashingTypeHeader, hashType.ToString());
    public static SessionHeader ClientId(Guid clientId) => new(ClientIdHeader, clientId.ToString());
    public static SessionHeader ClientName(string clientName) => new(ClientNameHeader, clientName);
    public static SessionHeader ExchangeType(ExchangeType exchangeType) => new(ExchangeTypeHeader, exchangeType.ToString());
    public static SessionHeader RingName(string ringName) => new(RingNameHeader, ringName);
}

public class SessionHeadersCollection
{
    private readonly ICollection<SessionHeader> _headersSource;
    public Dictionary<string, string> Headers => _headersSource.ToDictionary(x => x.Key, x => x.Value);

    public SessionHeadersCollection(IEnumerable<SessionHeader> headers)
    {
        _headersSource = headers.ToArray();
    }

    public SessionHeadersCollection(byte[] rawHeaders)
    {
        _headersSource = Encoding.UTF8.GetString(rawHeaders)
            .Split(';')
            .Select(x => new SessionHeader(x))
            .ToArray();
    }

    public byte[] ToRawHeaders()
    {
        return Encoding.UTF8.GetBytes(string.Join(';', _headersSource.Select(x => x.ToString())));
    }
}

public enum ExchangeType
{
    Pull,
    Push
}