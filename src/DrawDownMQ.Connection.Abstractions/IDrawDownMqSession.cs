namespace DrawDownMQ.Connection.Abstractions;

public interface IDrawDownMqSession : IDisposable
{
    Guid SessionId { get; }
    Task Connect(CancellationToken cancellationToken);
    Task StartAsync(CancellationToken cancellationToken);
    Task Send(Guid key, Memory<byte> value, CancellationToken cancellationToken);
}