using System.Net.Sockets;
using DrawDownMQ.Connection.Abstractions;
using Microsoft.Extensions.Logging;

namespace DrawDownMQ.Connection.Presentation;

public class MessagePresentation : IMessagePresentation
{
    private const int KeyLength = 16;
    private const int IntLength = 4;
    
    private readonly ICompressionProvider _compressionProvider;
    private readonly IEncryptionProvider _encryptionProvider;
    private readonly IHashProvider _hashProvider;
    private readonly Socket _socket;
    private readonly ILogger _logger;

    public MessagePresentation(
        Socket socket,
        ICompressionProvider compressionProvider,
        IEncryptionProvider encryptionProvider,
        IHashProvider hashProvider,
        ILogger logger,
        int parallelLevel = 128)
    {
        _socket = socket;
        _compressionProvider = compressionProvider;
        _encryptionProvider = encryptionProvider;
        _hashProvider = hashProvider;
        _logger = logger;
    }

    public async Task<byte[]> BuildMessage(Guid key, byte[] message, CancellationToken cancellationToken)
    {
        var keyBytes = key.ToByteArray();
        var hash = await _hashProvider.GetHash(message, cancellationToken);
        var encryptedMessage = await _encryptionProvider.Encrypt(message, cancellationToken);
        var compressedMessage = await _compressionProvider.Compress(encryptedMessage, cancellationToken);
        var contentLength = BitConverter.GetBytes(compressedMessage.Length);

        var fullLength = KeyLength + compressedMessage.Length + IntLength + _hashProvider.HashSize;
        var resultMessage = new byte[fullLength];
        
        for (var i = 0; i < KeyLength; i++)
        {
            resultMessage[i] = keyBytes[i];
        }

        for (var i = 0; i < IntLength; i++)
        {
            resultMessage[i + KeyLength] = contentLength[i];
        }

        for (var i = 0; i < compressedMessage.Length; i++)
        {
            resultMessage[i + IntLength + KeyLength] = compressedMessage[i];
        }

        for (var i = 0; i < _hashProvider.HashSize; i++)
        {
            resultMessage[i + IntLength + KeyLength + compressedMessage.Length] = hash[i];
        }

        return resultMessage;
    }

    public async Task<byte[]> ReceiveMessage(CancellationToken cancellationToken)
    {
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

        return encryptedMessage;
    }
}