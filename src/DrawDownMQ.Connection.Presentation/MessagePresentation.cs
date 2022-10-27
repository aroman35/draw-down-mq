using System.Buffers;
using System.Net.Sockets;
using DrawDownMQ.Connection.Abstractions;
using Microsoft.Extensions.Logging;

namespace DrawDownMQ.Connection.Presentation;

public class MessagePresentation : IMessagePresentation
{
    private const int KeyLength = 16;
    private const int IntLength = 4;
    
    private readonly ICompressionProvider _compressionProvider;
    private readonly IHashProvider _hashProvider;
    private readonly Socket _socket;
    private readonly ILogger _logger;
    private readonly IApplicationContext _applicationContext;

    public MessagePresentation(
        Socket socket,
        ICompressionProvider compressionProvider,
        IHashProvider hashProvider,
        ILogger logger)
    {
        _socket = socket;
        _compressionProvider = compressionProvider;
        _hashProvider = hashProvider;
        _logger = logger;
        _applicationContext = new TestContext(_logger);
    }

    public async Task<byte[]> BuildMessage(Guid key, byte[] message, CancellationToken cancellationToken)
    {
        try
        {
            var keyBytes = key.ToByteArray();
            var hash = await _hashProvider.GetHash(message, cancellationToken);
            var compressedMessage = await _compressionProvider.Compress(message, cancellationToken);
            var compressedLength = BitConverter.GetBytes(compressedMessage.Length);
            var messageLength = BitConverter.GetBytes(message.Length);

            var fullLength = KeyLength + compressedMessage.Length + IntLength * 2 + _hashProvider.HashSize;
            var resultMessage = new byte[fullLength];

            var bytesWrote = 0;
            for (var i = 0; i < KeyLength; i++)
            {
                resultMessage[i] = keyBytes[i];
            }

            bytesWrote += KeyLength;
            for (var i = 0; i < IntLength; i++)
            {
                resultMessage[i + bytesWrote] = compressedLength[i];
            }
            bytesWrote += IntLength;

            for (var i = 0; i < IntLength; i++)
            {
                resultMessage[i + bytesWrote] = messageLength[i];
            }
            bytesWrote += IntLength;

            for (var i = 0; i < compressedMessage.Length; i++)
            {
                resultMessage[i + bytesWrote] = compressedMessage[i];
            }
            bytesWrote += compressedMessage.Length;

            for (var i = 0; i < _hashProvider.HashSize; i++)
            {
                resultMessage[i + bytesWrote] = hash[i];
            }

            return resultMessage;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error sending message");
            throw;
        }
    }

    public async Task ReceiveMessage(CancellationToken cancellationToken)
    {
        
        var available = _socket.Available;
        if (available == 0)
            return;
        var buffer = MemoryPool<byte>.Shared.Rent(available);
        var messageSizeBytesRead = await _socket.ReceiveAsync(buffer.Memory, SocketFlags.None, cancellationToken);
        var bytesHandled = 0;
        var messagesRead = 0;
        while (bytesHandled < messageSizeBytesRead)
        {
            var keyBytes = buffer.Memory.Slice(bytesHandled, KeyLength);
            bytesHandled += KeyLength;
            var key = new Guid(keyBytes.Span);
            
            var compressedMessageLengthBuffer = buffer.Memory.Slice(bytesHandled, IntLength);
            bytesHandled += IntLength;
            var decompressedMessageLengthBuffer = buffer.Memory.Slice(bytesHandled, IntLength);
            bytesHandled += IntLength;

            var compressedMessageLength = BitConverter.ToInt32(compressedMessageLengthBuffer.Span);
            var decompressedMessageLength = BitConverter.ToInt32(decompressedMessageLengthBuffer.Span);

            var compressedMessage = buffer.Memory.Slice(bytesHandled, compressedMessageLength);
            bytesHandled += compressedMessageLength;
            var hash = buffer.Memory.Slice(bytesHandled, _hashProvider.HashSize);
            bytesHandled += _hashProvider.HashSize;
            
            var messageBuffer = MemoryPool<byte>.Shared.Rent(decompressedMessageLength);
            await _compressionProvider.Decompress(compressedMessage, messageBuffer.Memory, cancellationToken);
            var decompressedMessage = messageBuffer.Memory[..decompressedMessageLength];

            var isHashCorrect = _hashProvider.CompareHash(decompressedMessage.Span, hash.Span);
            if (!isHashCorrect)
                throw new ApplicationException("Hash is not correct");

            await _applicationContext.MessageReceived(DrawDownMqTransportMessage.Create(
                    key,
                    hash.Span,
                    messageBuffer.Memory[..decompressedMessageLength].Span),
                cancellationToken);
            messagesRead++;
        }
        _logger.LogInformation("Total read: {Count} messages", messagesRead);
    }
}