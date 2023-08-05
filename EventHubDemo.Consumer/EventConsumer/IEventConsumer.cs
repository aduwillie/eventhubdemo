namespace EventHubDemo.Consumer.EventConsumer;

internal interface IEventConsumer
{
    Task Consume(CancellationToken cancellationToken = default);
}
