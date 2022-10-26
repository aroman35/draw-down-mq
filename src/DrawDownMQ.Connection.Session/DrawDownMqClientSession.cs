using System.Net;
using System.Net.Sockets;
using System.Text;
using DrawDownMQ.Connection.Abstractions;
using DrawDownMQ.Connection.Common;
using Microsoft.Extensions.Logging;

namespace DrawDownMQ.Connection.Session;

public class DrawDownMqClientSession : IDrawDownMqSession
{
    private readonly Socket _socket;
    private readonly IPEndPoint _serverEndpoint;
    private readonly SessionHeadersCollection _sessionHeaders;
    private readonly ICompressionSwitcher _compressionSwitcher;
    private readonly IEncryptionSwitcher _encryptionSwitcher;
    private readonly IHashSwitcher _hashSwitcher;
    private readonly ILogger _logger;
    public Guid SessionId => Guid.Parse(_sessionHeaders.Headers[SessionHeader.ClientIdHeader]);
    
    private ICompressionProvider _compressionProvider;
    private IEncryptionProvider _encryptionProvider;
    private IHashProvider _hashProvider;

    private DrawDownMqClientSession(
        Socket socket,
        IPEndPoint serverEndpoint,
        SessionHeadersCollection sessionHeaders,
        ICompressionSwitcher compressionSwitcher,
        IHashSwitcher hashSwitcher,
        IEncryptionSwitcher encryptionSwitcher,
        ILogger logger)
    {
        _logger = logger;
        _encryptionSwitcher = encryptionSwitcher;
        _hashSwitcher = hashSwitcher;
        _compressionSwitcher = compressionSwitcher;
        _sessionHeaders = sessionHeaders;
        _serverEndpoint = serverEndpoint;
        _socket = socket;
    }

    public static async Task<IDrawDownMqSession> Create(
        IPEndPoint serverEndpoint,
        SessionHeadersCollection sessionHeaders,
        ICompressionSwitcher compressionSwitcher,
        IHashSwitcher hashSwitcher,
        IEncryptionSwitcher encryptionSwitcher,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        var client = new DrawDownMqClientSession(
            socket,
            serverEndpoint,
            sessionHeaders,
            compressionSwitcher,
            hashSwitcher,
            encryptionSwitcher,
            logger);
        
        await client.Connect(cancellationToken);

        return client;
    }
    
    public async Task Connect(CancellationToken cancellationToken)
    {
        await _socket.ConnectAsync(_serverEndpoint, cancellationToken);
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("Connected to {Server}:{Port}", _serverEndpoint.Address, _serverEndpoint.Port);
        
        await SendHeaders(cancellationToken);
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("Client headers were sent");

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

    private async Task SendHeaders(CancellationToken cancellationToken)
    {
        var rawHeaders = _sessionHeaders.ToRawHeaders();
        var rawHeadersLength = BitConverter.GetBytes(rawHeaders.Length);
        await _socket.SendAsync(rawHeadersLength, SocketFlags.None, cancellationToken);
        await _socket.SendAsync(rawHeaders, SocketFlags.None, cancellationToken);
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

    // TODO: Presentation layer
    private async Task HandleMessage(byte[] keyBytes, CancellationToken cancellationToken)
    {
        //TODO: use array pool for buffers
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
        
        // TODO: For test purpose only. Application layer should handle the message
        var messageString = Encoding.UTF8.GetString(encryptedMessage);
        _logger.LogInformation("[Client]: Read message: {Message} with key: {Key}", messageString, key);
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

    public void Dispose()
    {
        _socket.Dispose();
    }
}