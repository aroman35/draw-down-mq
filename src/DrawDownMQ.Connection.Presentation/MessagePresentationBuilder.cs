using System.Net.Sockets;
using DrawDownMQ.Connection.Abstractions;
using Microsoft.Extensions.Logging;

namespace DrawDownMQ.Connection.Presentation;

public class MessagePresentationBuilder : IMessagePresentationBuilder
{
    private readonly ICompressionSwitcher _compressionSwitcher;
    private readonly IHashSwitcher _hashSwitcher;
    private readonly ILogger _logger;

    public MessagePresentationBuilder(
        ICompressionSwitcher compressionSwitcher,
        IHashSwitcher hashSwitcher,
        ILogger logger)
    {
        _logger = logger;
        _compressionSwitcher = compressionSwitcher;
        _hashSwitcher = hashSwitcher;
    }

    public IMessagePresentation Build(Socket socket, Action<MessageHandlerOption> options)
    {
        var builtOptions = new MessageHandlerOption();
        options.Invoke(builtOptions);

        var compression = _compressionSwitcher.Create(builtOptions.CompressionType);
        var hash = _hashSwitcher.Create(builtOptions.HashType);

        var presentation = new MessagePresentation(socket, compression, hash, _logger);
        return presentation;
    }
}