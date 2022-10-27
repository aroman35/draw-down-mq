namespace DrawDownMQ.Connection.Abstractions;

public interface IMessagePresentation
{
    int BuildMessage(Guid key, Memory<byte> message, Memory<byte> output);
    Task ReceiveMessage(CancellationToken cancellationToken);
    int ApproximateMessageSize(int messageLength);
}