namespace EventHubDemo.Publisher.Application;

internal interface IEventPublisher
{
    Task Publish(CancellationToken cancellationToken = default);
}
