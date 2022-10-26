using System.Net;
using DrawDownMQ.Connection.Abstractions;
using DrawDownMQ.Connection.Common;
using Microsoft.Extensions.Logging;

namespace DrawDownMQ.Connection.Transport;

public class DrawDownMqClient : IDrawDownMqListener
{
    private readonly IPEndPoint _serverEndpoint;
    private readonly SessionHeadersCollection _headersCollection;
    private readonly ISessionsManager _sessionsManager;
    private readonly ILogger _logger;
    private IDrawDownMqSession _clientSession;

    public DrawDownMqClient(
        IPEndPoint serverEndpoint,
        SessionHeadersCollection headersCollection,
        ISessionsManager sessionsManager,
        ILogger logger)
    {
        _logger = logger;
        _sessionsManager = sessionsManager;
        _serverEndpoint = serverEndpoint;
        _headersCollection = headersCollection;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _clientSession = await _sessionsManager.CreateClient(_serverEndpoint, _headersCollection, cancellationToken);
        await Task.Factory.StartNew(() => _clientSession.StartAsync(cancellationToken), cancellationToken);
    }

    public async Task SendAsync(Guid key, byte[] message, CancellationToken cancellationToken)
    {
        await _clientSession.Send(key, message, cancellationToken);
    }

    public void Dispose()
    {
        _sessionsManager.Dispose();
        GC.SuppressFinalize(this);
    }
}