using System.Net.Sockets;
using System.Text;
using DrawDownMQ.Connection.Abstractions;
using DrawDownMQ.Connection.Common;
using Microsoft.Extensions.Logging;

namespace DrawDownMQ.Connection.Session;

public class DrawDownMqServerSession : IDrawDownMqSession
{
    private readonly Socket _socket;
    private readonly ICompressionSwitcher _compressionSwitcher;
    private readonly IEncryptionSwitcher _encryptionSwitcher;
    private readonly IHashSwitcher _hashSwitcher;
    private readonly ILogger _logger;
    public Guid SessionId { get; private set; }

    private ICompressionProvider _compressionProvider;
    private IEncryptionProvider _encryptionProvider;
    private IHashProvider _hashProvider;
    private SessionHeadersCollection _sessionHeaders;
    
    private DrawDownMqServerSession(
        Socket socket,
        ICompressionSwitcher compressionSwitcher,
        IEncryptionSwitcher encryptionSwitcher,
        IHashSwitcher hashSwitcher,
        ILogger logger)
    {
        _socket = socket;
        _logger = logger;
        _encryptionSwitcher = encryptionSwitcher;
        _hashSwitcher = hashSwitcher;
        _compressionSwitcher = compressionSwitcher;
    }

    public static async Task<DrawDownMqServerSession> Create(
        Socket socket,
        ICompressionSwitcher compressionSwitcher,
        IEncryptionSwitcher encryptionSwitcher,
        IHashSwitcher hashSwitcher,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var session = new DrawDownMqServerSession(socket, compressionSwitcher, encryptionSwitcher, hashSwitcher, logger);
        await session.Connect(cancellationToken);

        return session;
    }
    
    public async Task Connect(CancellationToken cancellationToken)
    {
        await HandleHeaders(cancellationToken);
    }

    private async Task HandleHeaders(CancellationToken cancellationToken)
    {
        var buffer = new byte[4];
        await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
        var headersLength = BitConverter.ToInt32(buffer);
        var headersBuffer = new byte[headersLength];
        await _socket.ReceiveAsync(headersBuffer, SocketFlags.None, cancellationToken);
        _sessionHeaders = new SessionHeadersCollection(headersBuffer);
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Received {Count} headers", _sessionHeaders.Headers.Count);
        }

        if (!Guid.TryParse(_sessionHeaders.Headers[SessionHeader.ClientIdHeader], out var clientId))
            throw new ApplicationException("Client id undefined");

        SessionId = clientId;
        
        var _ = _sessionHeaders.Headers.TryGetValue(SessionHeader.CompressionHeader, out var compressionValue)
                & Enum.TryParse<CompressionType>(compressionValue, true, out var compressionType);

        _ = _sessionHeaders.Headers.TryGetValue(SessionHeader.EncryptionHeader, out var encryptionValue)
            & Enum.TryParse<EncryptionType>(encryptionValue, true, out var encryptionType);

        _ = _sessionHeaders.Headers.TryGetValue(SessionHeader.HashingTypeHeader, out var hashingValue)
            & Enum.TryParse<HashType>(hashingValue, true, out var hashingType);
        
        SetCompression(compressionType);
        SetEncryption(encryptionType);
        SetHashing(hashingType);
    }
    
    private void SetCompression(CompressionType compressionType)
    {
        _compressionProvider = _compressionSwitcher.Create(compressionType);
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("Compression level is switched to {CompressionType}", compressionType);
    }
    
    private void SetEncryption(EncryptionType encryptionType)
    {
        _encryptionProvider = _encryptionSwitcher.Create(encryptionType);
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("Encryption set to {EncryptionType}", encryptionType);
    }

    private void SetHashing(HashType hashType)
    {
        _hashProvider = _hashSwitcher.Create(hashType);
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("Hashing set to {HashType}", hashType);
    }
    
    public async Task Send(Guid key, byte[] value, CancellationToken cancellationToken)
    {
        var message = await BuildMessage(key, value, cancellationToken);
        await _socket.SendAsync(message, SocketFlags.None, cancellationToken);
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("Message with {MessageLength} bytes was sent", message.Length);
    }

    private const int KeyLength = 16;
    private const int IntLength = 4;
    
    private async Task<byte[]> BuildMessage(Guid key, byte[] value, CancellationToken cancellationToken)
    {
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
    
    private async Task HandleMessage(byte[] keyBytes, CancellationToken cancellationToken)
    {
        var key = new Guid(keyBytes);
        var messageSizeBuffer = new byte[IntLength];
        var messageSizeBytesRead = await _socket.ReceiveAsync(messageSizeBuffer, SocketFlags.None, cancellationToken);
        if (messageSizeBytesRead != IntLength)
            throw new ApplicationException("Message size incomplete");
        var messageSize = BitConverter.ToInt32(messageSizeBuffer);
        var messageBuffer = new byte[messageSize];
        var messageSizeRead = await _socket.ReceiveAsync(messageBuffer, SocketFlags.None, cancellationToken);
        if (messageSizeRead != messageSize)
            throw new ApplicationException("Message incomplete");
        var hashBuffer = new byte[_hashProvider.HashSize];
        var hashBytesRead = await _socket.ReceiveAsync(hashBuffer, SocketFlags.None, cancellationToken);
        if (hashBytesRead != _hashProvider.HashSize)
            throw new ApplicationException("Hash incomplete");
        var decompressedMessage = await _compressionProvider.Decompress(messageBuffer, cancellationToken);
        var encryptedMessage = await _encryptionProvider.Decrypt(decompressedMessage, cancellationToken);
        if (!await _hashProvider.CompareHash(encryptedMessage, hashBuffer, cancellationToken))
            throw new ApplicationException("Message hash not match");
        
        var messageString = Encoding.UTF8.GetString(encryptedMessage);
        
        _logger.LogInformation("[Server]: Read message: {Message} with key: {Key}", messageString, key);
        var reply = Encoding.UTF8.GetBytes("Fuck off!");
        await Send(key, reply, cancellationToken);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var keyBuffer = new byte[KeyLength];
            var bytesReceived = await _socket.ReceiveAsync(keyBuffer, SocketFlags.None, cancellationToken);
            if (bytesReceived == KeyLength)
            {
                await HandleMessage(keyBuffer, cancellationToken);
            }
        }
    }
    
    public void Dispose()
    {
        _socket.Dispose();
    }
}