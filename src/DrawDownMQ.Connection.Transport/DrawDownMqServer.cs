using System.Net;
using System.Net.Sockets;
using DrawDownMQ.Connection.Abstractions;
using Microsoft.Extensions.Logging;

namespace DrawDownMQ.Connection.Transport;

public class DrawDownMqServer : IDrawDownMqListener
{
    private readonly TcpListener _tcpListener;
    private readonly ISessionsManager _sessionsManager;
    private readonly ILogger _logger;

    public DrawDownMqServer(IPEndPoint ipEndPoint, ISessionsManager sessionsManager, ILogger logger)
    {
        if (ipEndPoint == null) throw new ArgumentNullException(nameof(ipEndPoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionsManager = sessionsManager ?? throw new ArgumentNullException(nameof(sessionsManager));

        _tcpListener = new TcpListener(ipEndPoint);
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("Created server {Ip}:{Port}", ipEndPoint.Address.ToString(), ipEndPoint.Port);
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _tcpListener.Start();
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("Started server");
        
        await Task.Factory.StartNew(() => RunAsync(cancellationToken), cancellationToken);
    }

    public async Task SendAsync(Guid key, byte[] message, CancellationToken cancellationToken)
    {
        // TODO: needs to add addressing info for sending direct message
        throw new NotImplementedException();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_tcpListener.Pending())
            {
                var socket = await _tcpListener.AcceptSocketAsync(cancellationToken);
                var session = await _sessionsManager.ConnectClient(socket, cancellationToken);
                await Task.Factory.StartNew(() => session.StartAsync(cancellationToken), cancellationToken);
                if (_logger.IsEnabled(LogLevel.Trace))
                    _logger.LogTrace("Created session");
            }
        }
    }
    
    public void Dispose()
    {
        _sessionsManager.Dispose();
        _tcpListener.Stop();
        GC.SuppressFinalize(this);
    }
}