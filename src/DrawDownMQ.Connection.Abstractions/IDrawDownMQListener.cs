namespace DrawDownMQ.Connection.Abstractions;

public interface IDrawDownMqListener : IDisposable
{
    Task StartAsync(CancellationToken cancellationToken);
    Task SendAsync(Guid key, byte[] message, CancellationToken cancellationToken);
}