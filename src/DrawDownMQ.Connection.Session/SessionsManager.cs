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
    private readonly IMessagePresentationBuilder _presentationBuilder;

    public SessionsManager(
        IMessagePresentationBuilder presentationBuilder,
        ILogger logger)
    {
        _logger = logger;
        _presentationBuilder = presentationBuilder;
        _runningSessions = new ConcurrentDictionary<Guid, IDrawDownMqSession>();
    }

    public async Task<IDrawDownMqSession> ConnectClient(Socket socket, CancellationToken cancellationToken)
    {
        var session = await DrawDownMqServerSession.Create(
            socket,
            _presentationBuilder,
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
            _presentationBuilder,
            _logger,
            cancellationToken);
        
        _runningSessions[session.SessionId] = session;
        return session;
    }

    public IDrawDownMqSession GetSession(Guid sessionId)
    {
        if (!_runningSessions.TryGetValue(sessionId, out var session))
            throw new ArgumentException($"Session {sessionId} was not found");
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