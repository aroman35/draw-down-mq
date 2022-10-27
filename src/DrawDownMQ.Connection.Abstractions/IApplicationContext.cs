using Microsoft.Extensions.Logging;

namespace DrawDownMQ.Connection.Abstractions;

public interface IApplicationContext
{
    Task MessageReceived(DrawDownMqTransportMessage message, CancellationToken cancellationToken);
    Task SendMessage(DrawDownMqTransportMessage message, CancellationToken cancellationToken);
}

public class TestContext : IApplicationContext
{
    private readonly ILogger _logger;

    public TestContext(ILogger logger)
    {
        _logger = logger;
    }

    public Task MessageReceived(DrawDownMqTransportMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received {Message}", message.ToString());
        return Task.CompletedTask;
    }

    public Task SendMessage(DrawDownMqTransportMessage message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}