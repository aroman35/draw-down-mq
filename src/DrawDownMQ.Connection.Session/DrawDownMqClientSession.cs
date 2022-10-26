using System.Net;
using System.Net.Sockets;
using DrawDownMQ.Connection.Abstractions;
using DrawDownMQ.Connection.Common;
using Microsoft.Extensions.Logging;

namespace DrawDownMQ.Connection.Session;

public class DrawDownMqClientSession : DrawDownMqSession
{
    private readonly IPEndPoint _serverEndpoint;

    private DrawDownMqClientSession(
        Socket socket,
        IPEndPoint serverEndpoint,
        SessionHeadersCollection sessionHeaders,
        IMessagePresentationBuilder presentationBuilder,
        ILogger logger) : base(socket, presentationBuilder, logger)
    {
        SessionHeaders = sessionHeaders;
        _serverEndpoint = serverEndpoint;
    }

    public static async Task<IDrawDownMqSession> Create(
        IPEndPoint serverEndpoint,
        SessionHeadersCollection sessionHeaders,
        IMessagePresentationBuilder presentationBuilder,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        var client = new DrawDownMqClientSession(
            socket,
            serverEndpoint,
            sessionHeaders,
            presentationBuilder,
            logger);
        
        await client.Connect(cancellationToken);

        return client;
    }
    
    public override async Task Connect(CancellationToken cancellationToken)
    {
        await Socket.ConnectAsync(_serverEndpoint, cancellationToken);
        if (Logger.IsEnabled(LogLevel.Trace))
            Logger.LogTrace("Connected to {Server}:{Port}", _serverEndpoint.Address, _serverEndpoint.Port);
        
        await SendHeaders(cancellationToken);
        if (Logger.IsEnabled(LogLevel.Trace))
            Logger.LogTrace("Client headers were sent");

        SetServicesFromHeaders();
    }

    private async Task SendHeaders(CancellationToken cancellationToken)
    {
        var rawHeaders = SessionHeaders.ToRawHeaders();
        var rawHeadersLength = BitConverter.GetBytes(rawHeaders.Length);
        await Socket.SendAsync(rawHeadersLength, SocketFlags.None, cancellationToken);
        await Socket.SendAsync(rawHeaders, SocketFlags.None, cancellationToken);
    }
}