namespace DrawDownMQ.Connection.Abstractions;

public interface IMessagePresentation
{
    Task<byte[]> BuildMessage(Guid key, byte[] message, CancellationToken cancellationToken);
    Task ReceiveMessage(CancellationToken cancellationToken);
}