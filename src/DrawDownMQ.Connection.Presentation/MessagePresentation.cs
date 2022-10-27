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

    public int ApproximateMessageSize(int messageLength)
    {
        return messageLength + 2 * IntLength + KeyLength + _hashProvider.HashSize;
    }
    
    public int BuildMessage(Guid key, Memory<byte> message, Memory<byte> output)
    {
        try
        {
            using var pool = MemoryPool<byte>.Shared;
            Span<byte> keyBytes = key.ToByteArray();
            var hash = pool.Rent(_hashProvider.HashSize).Memory[.._hashProvider.HashSize];
            _hashProvider.ComputeHash(message.Span, hash.Span);
            var compressionBuffer = pool.Rent(message.Length);
            var compressedMessageLength = _compressionProvider.Compress(message.Span, compressionBuffer.Memory.Span);
            Span<byte> compressedLength = BitConverter.GetBytes(compressedMessageLength);
            Span<byte> messageLength = BitConverter.GetBytes(message.Length);

            var fullLength = KeyLength + compressedMessageLength + IntLength * 2 + _hashProvider.HashSize;

            var bytesWrote = 0;
            keyBytes.CopyTo(output.Span, bytesWrote);
            bytesWrote += KeyLength;
            compressedLength.CopyTo(output.Span, bytesWrote);
            bytesWrote += IntLength;
            messageLength.CopyTo(output.Span, bytesWrote);
            bytesWrote += IntLength;
            compressionBuffer.Memory.Span[..compressedMessageLength].CopyTo(output.Span, bytesWrote);
            bytesWrote += compressedMessageLength;
            hash.Span.CopyTo(output.Span, bytesWrote);
            
            if (_logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace("Message with {Length} bytes was built", fullLength);

            return fullLength;
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
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("Received {Length} bytes", messageSizeBytesRead);
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
            _compressionProvider.Decompress(compressedMessage.Span, messageBuffer.Memory.Span);
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