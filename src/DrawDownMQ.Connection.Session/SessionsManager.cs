using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using DrawDownMQ.Connection.Abstractions;
using DrawDownMQ.Connection.Common;
using Microsoft.Extensions.Logging;

namespace DrawDownMQ.Connection.Session;

public class SessionsManager : ISessionsManager
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<Guid, IDrawDownMqSession> _runningSessions;
    private readonly ICompressionSwitcher _compressionSwitcher;
    private readonly IHashSwitcher _hashSwitcher;
    private readonly IEncryptionSwitcher _encryptionSwitcher;

    public SessionsManager(
        ICompressionSwitcher compressionSwitcher,
        IHashSwitcher hashSwitcher,
        IEncryptionSwitcher encryptionSwitcher,
        ILogger logger)
    {
        _logger = logger;
        _hashSwitcher = hashSwitcher;
        _encryptionSwitcher = encryptionSwitcher;
        _compressionSwitcher = compressionSwitcher;
        _runningSessions = new ConcurrentDictionary<Guid, IDrawDownMqSession>();
    }

    public async Task<IDrawDownMqSession> ConnectClient(Socket socket, CancellationToken cancellationToken)
    {
        var session = await DrawDownMqServerSession.Create(
            socket,
            _compressionSwitcher,
            _encryptionSwitcher,
            _hashSwitcher,
            _logger,
            cancellationToken);
        
        _runningSessions[session.SessionId] = session;

        return session;
    }

    public async Task<IDrawDownMqSession> CreateClient(
        IPEndPoint serverEndpoint,
        SessionHeadersCollection sessionHeaders,
        CancellationToken cancellationToken)
    {
        var session = await DrawDownMqClientSession.Create(
            serverEndpoint,
            sessionHeaders,
            _compressionSwitcher,
            _hashSwitcher,
            _encryptionSwitcher,
            _logger,
            cancellationToken);
        
        _runningSessions[session.SessionId] = session;
        return session;
    }
    
    public void Dispose()
    {
        foreach (var drawDownMqSession in _runningSessions.Values)
        {
            drawDownMqSession.Dispose();
        }
    }
}