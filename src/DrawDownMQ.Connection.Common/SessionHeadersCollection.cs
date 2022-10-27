using System.Text;

namespace DrawDownMQ.Connection.Common;

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