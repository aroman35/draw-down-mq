using System.Net.Sockets;
using DrawDownMQ.Connection.Abstractions;
using DrawDownMQ.Connection.Common;
using Microsoft.Extensions.Logging;

namespace DrawDownMQ.Connection.Session;

public class DrawDownMqServerSession : DrawDownMqSession
{
    private DrawDownMqServerSession(
        Socket socket,
        IMessagePresentationBuilder presentationBuilder,
        ILogger logger) : base(socket, presentationBuilder, logger)
    {
    }

    public static async Task<DrawDownMqServerSession> Create(
        Socket socket,
        IMessagePresentationBuilder presentationBuilder,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var session = new DrawDownMqServerSession(socket, presentationBuilder, logger);
        await session.Connect(cancellationToken);

        return session;
    }
    
    public override async Task Connect(CancellationToken cancellationToken)
    {
        await HandleHeaders(cancellationToken);
    }

    private async Task HandleHeaders(CancellationToken cancellationToken)
    {
        var buffer = new byte[4];
        await Socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
        var headersLength = BitConverter.ToInt32(buffer);
        var headersBuffer = new byte[headersLength];
        await Socket.ReceiveAsync(headersBuffer, SocketFlags.None, cancellationToken);
        SessionHeaders = new SessionHeadersCollection(headersBuffer);
        if (Logger.IsEnabled(LogLevel.Trace))
        {
            Logger.LogTrace("Received {Count} headers", SessionHeaders.Headers.Count);
        }

        SetServicesFromHeaders();
    }
}