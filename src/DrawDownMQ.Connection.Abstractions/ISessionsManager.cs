using System.Net;
using System.Net.Sockets;
using DrawDownMQ.Connection.Common;

namespace DrawDownMQ.Connection.Abstractions;

public interface ISessionsManager : IDisposable
{
    Task<IDrawDownMqSession> ConnectClient(Socket socket, CancellationToken cancellationToken);

    Task<IDrawDownMqSession> CreateClient(IPEndPoint serverEndpoint,
        SessionHeadersCollection sessionHeaders,
        CancellationToken cancellationToken);
}