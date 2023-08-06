namespace EventHubDemo.Consumer.EventConsumer;

internal interface IEventConsumer : IAsyncDisposable
{
    Task Consume(CancellationToken cancellationToken = default);
}
