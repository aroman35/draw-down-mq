using System.Net.Sockets;
using System.Text;
using DrawDownMQ.Connection.Abstractions;
using DrawDownMQ.Connection.Common;
using Microsoft.Extensions.Logging;

namespace DrawDownMQ.Connection.Session;

public abstract class DrawDownMqSession : IDrawDownMqSession
{
    private const int KeySize = 16;
    private readonly IMessagePresentationBuilder _presentationBuilder;
    
    private protected readonly Socket Socket;
    private protected readonly ILogger Logger;
    private protected SessionHeadersCollection SessionHeaders;

    private IMessagePresentation _messagePresentation;

    public Guid SessionId => Guid.Parse(SessionHeaders.Headers[SessionHeader.ClientIdHeader]);

    protected DrawDownMqSession(
        Socket socket,
        IMessagePresentationBuilder presentationBuilder,
        ILogger logger)
    {
        Socket = socket;
        Logger = logger;
        _presentationBuilder = presentationBuilder;
    }

    public abstract Task Connect(CancellationToken cancellationToken);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var keyBuffer = new byte[KeySize];
            var bytesReceived = await Socket.ReceiveAsync(keyBuffer, SocketFlags.None, cancellationToken);
            if (bytesReceived == KeySize)
            {
                var messageReceived = await _messagePresentation.ReceiveMessage(cancellationToken);
                Logger.LogInformation("Received message {Message}", Encoding.UTF8.GetString(messageReceived));
            }
        }
    }

    public async Task Send(Guid key, byte[] value, CancellationToken cancellationToken)
    {
        var message = await _messagePresentation.BuildMessage(key, value, cancellationToken);
        
        await Socket.SendAsync(message, SocketFlags.None, cancellationToken);
        if (Logger.IsEnabled(LogLevel.Trace))
            Logger.LogTrace("Message with {MessageLength} bytes was sent", message.Length);
    }

    private protected void SetServicesFromHeaders()
    {
        var _ = SessionHeaders.Headers.TryGetValue(SessionHeader.CompressionHeader, out var compressionValue)
                & Enum.TryParse<CompressionType>(compressionValue, true, out var compressionType);

        _ = SessionHeaders.Headers.TryGetValue(SessionHeader.EncryptionHeader, out var encryptionValue)
            & Enum.TryParse<EncryptionType>(encryptionValue, true, out var encryptionType);

        _ = SessionHeaders.Headers.TryGetValue(SessionHeader.HashingTypeHeader, out var hashingValue)
            & Enum.TryParse<HashType>(hashingValue, true, out var hashingType);

        _messagePresentation = _presentationBuilder.Build(Socket, options =>
        {
            options.CompressionType = compressionType;
            options.EncryptionType = encryptionType;
            options.HashType = hashingType;
        });
    }
    
    public virtual void Dispose()
    {
        Socket?.Dispose();
    }
}