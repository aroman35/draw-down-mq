using System.Net.Sockets;
using System.Text;
using DrawDownMQ.Connection.Abstractions;
using DrawDownMQ.Connection.Common;
using Microsoft.Extensions.Logging;

namespace DrawDownMQ.Connection.Session;

public abstract class DrawDownMqSession : IDrawDownMqSession
{
    private protected readonly Socket Socket;
    private readonly ICompressionSwitcher _compressionSwitcher;
    private readonly IEncryptionSwitcher _encryptionSwitcher;
    private readonly IHashSwitcher _hashSwitcher;
    private protected readonly ILogger Logger;
    
    private protected SessionHeadersCollection SessionHeaders;
    private ICompressionProvider _compressionProvider;
    private IEncryptionProvider _encryptionProvider;
    private IHashProvider _hashProvider;
    
    public Guid SessionId => Guid.Parse(SessionHeaders.Headers[SessionHeader.ClientIdHeader]);

    protected DrawDownMqSession(
        Socket socket,
        ICompressionSwitcher compressionSwitcher,
        IEncryptionSwitcher encryptionSwitcher,
        IHashSwitcher hashSwitcher,
        ILogger logger)
    {
        Socket = socket;
        _compressionSwitcher = compressionSwitcher;
        _encryptionSwitcher = encryptionSwitcher;
        _hashSwitcher = hashSwitcher;
        Logger = logger;
    }

    public abstract Task Connect(CancellationToken cancellationToken);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var keyBuffer = new byte[KeyLength];
            var bytesReceived = await Socket.ReceiveAsync(keyBuffer, SocketFlags.None, cancellationToken);
            if (bytesReceived == KeyLength)
            {
                await HandleMessage(keyBuffer, cancellationToken);
            }
        }
    }

    // TODO: Presentation layer
    private async Task HandleMessage(byte[] keyBytes, CancellationToken cancellationToken)
    {
        //TODO: use array pool for buffers
        var key = new Guid(keyBytes);
        var messageSizeBuffer = new byte[IntLength];
        var messageSizeBytesRead = await Socket.ReceiveAsync(messageSizeBuffer, SocketFlags.None, cancellationToken);
        if (messageSizeBytesRead != IntLength)
            throw new ApplicationException("Message size incomplete");
        var messageSize = BitConverter.ToInt32(messageSizeBuffer);
        var messageBuffer = new byte[messageSize];
        var messageSizeRead = await Socket.ReceiveAsync(messageBuffer, SocketFlags.None, cancellationToken);
        if (messageSizeRead != messageSize)
            throw new ApplicationException("Message incomplete");
        var hashBuffer = new byte[_hashProvider.HashSize];
        var hashBytesRead = await Socket.ReceiveAsync(hashBuffer, SocketFlags.None, cancellationToken);
        if (hashBytesRead != _hashProvider.HashSize)
            throw new ApplicationException("Hash incomplete");
        var decompressedMessage = await _compressionProvider.Decompress(messageBuffer, cancellationToken);
        var encryptedMessage = await _encryptionProvider.Decrypt(decompressedMessage, cancellationToken);
        if (!await _hashProvider.CompareHash(encryptedMessage, hashBuffer, cancellationToken))
            throw new ApplicationException("Message hash not match");
        
        // TODO: For test purpose only. Application layer should handle the message
        var messageString = Encoding.UTF8.GetString(encryptedMessage);
        Logger.LogInformation("Read message: {Message} with key: {Key}", messageString, key);
    }

    public async Task Send(Guid key, byte[] value, CancellationToken cancellationToken)
    {
        var message = await BuildMessage(key, value, cancellationToken);
        await Socket.SendAsync(message, SocketFlags.None, cancellationToken);
        if (Logger.IsEnabled(LogLevel.Trace))
            Logger.LogTrace("Message with {MessageLength} bytes was sent", message.Length);
    }

    private const int KeyLength = 16;
    private const int IntLength = 4;
    
    // TODO: Presentation layer
    private async Task<byte[]> BuildMessage(Guid key, byte[] value, CancellationToken cancellationToken)
    {
        //TODO: use array pool
        var keyBytes = key.ToByteArray();
        var hash = await _hashProvider.GetHash(value, cancellationToken);
        var encryptedMessage = await _encryptionProvider.Encrypt(value, cancellationToken);
        var compressedMessage = await _compressionProvider.Compress(encryptedMessage, cancellationToken);
        var contentLength = BitConverter.GetBytes(compressedMessage.Length);

        var fullLength = KeyLength + compressedMessage.Length + IntLength + _hashProvider.HashSize;
        var message = new byte[fullLength];
        
        for (var i = 0; i < KeyLength; i++)
        {
            message[i] = keyBytes[i];
        }

        for (var i = 0; i < IntLength; i++)
        {
            message[i + KeyLength] = contentLength[i];
        }

        for (var i = 0; i < compressedMessage.Length; i++)
        {
            message[i + IntLength + KeyLength] = compressedMessage[i];
        }

        for (var i = 0; i < _hashProvider.HashSize; i++)
        {
            message[i + IntLength + KeyLength + compressedMessage.Length] = hash[i];
        }

        return message;
    }

    private protected void SetServicesFromHeaders()
    {
        var _ = SessionHeaders.Headers.TryGetValue(SessionHeader.CompressionHeader, out var compressionValue)
                & Enum.TryParse<CompressionType>(compressionValue, true, out var compressionType);

        _ = SessionHeaders.Headers.TryGetValue(SessionHeader.EncryptionHeader, out var encryptionValue)
            & Enum.TryParse<EncryptionType>(encryptionValue, true, out var encryptionType);

        _ = SessionHeaders.Headers.TryGetValue(SessionHeader.HashingTypeHeader, out var hashingValue)
            & Enum.TryParse<HashType>(hashingValue, true, out var hashingType);
        
        SetCompression(compressionType);
        SetEncryption(encryptionType);
        SetHashing(hashingType);
    }
    
    private void SetCompression(CompressionType compressionType)
    {
        _compressionProvider = _compressionSwitcher.Create(compressionType);
        if (Logger.IsEnabled(LogLevel.Trace))
            Logger.LogTrace("Compression level is switched to {CompressionType}", compressionType);
    }

    private void SetEncryption(EncryptionType encryptionType)
    {
        _encryptionProvider = _encryptionSwitcher.Create(encryptionType);
        if (Logger.IsEnabled(LogLevel.Trace))
            Logger.LogTrace("Encryption set to {EncryptionType}", encryptionType);
    }

    private void SetHashing(HashType hashType)
    {
        _hashProvider = _hashSwitcher.Create(hashType);
        if (Logger.IsEnabled(LogLevel.Trace))
            Logger.LogTrace("Hashing set to {HashType}", hashType);
    }
    
    public virtual void Dispose()
    {
        Socket?.Dispose();
    }
}