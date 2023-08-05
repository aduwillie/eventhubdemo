using Azure.Messaging.EventHubs.Producer;
using EventHubDemo.Publisher.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace EventHubDemo.Publisher.Application;

internal class EventPublisher : IEventPublisher
{
    private readonly ILogger<EventPublisher> logger;
    private readonly EventHubProducerClient eventHubProducerClient;
    private readonly EventProducerConfig eventProducerConfig;

    public EventPublisher(
        ILogger<EventPublisher> logger,
        EventHubProducerClient eventHubProducerClient,
        IOptionsMonitor<EventProducerConfig> optionsMonitor)
    {
        this.logger = logger;
        this.eventHubProducerClient = eventHubProducerClient;
        this.eventProducerConfig = optionsMonitor.CurrentValue;
    }

    public async Task Publish(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Publishing events...");

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await GenerateBatchEvents(eventProducerConfig.Count);
            await eventHubProducerClient.SendAsync(batch);
            logger.LogInformation("A batch of {totalEvents} total events sent", batch.Count);

            await Task.Delay(TimeSpan.FromSeconds(5)); // wait 5 seconds
        }
    }

    private async Task<EventDataBatch> GenerateBatchEvents(int batchSize)
    {
        var eventDataBatch = await eventHubProducerClient.CreateBatchAsync();

        for (var i = 0; i < batchSize; i++)
        {
            var randomGen = new Random();
            if (!eventDataBatch.TryAdd(new(Encoding.UTF8.GetBytes($"Event: {randomGen.Next(1, 1000)}"))))
            {
                logger.LogError($"Event {i} is too large for the batch and cannot be sent.");
            }
        }

        return eventDataBatch;
    }
}
