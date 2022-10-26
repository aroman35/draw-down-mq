using System.Net.Sockets;

namespace DrawDownMQ.Connection.Abstractions;

public interface IMessagePresentationBuilder
{
    IMessagePresentation Build(Socket socket, Action<MessageHandlerOption> options);
}