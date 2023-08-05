using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using EventHubDemo.Consumer.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace EventHubDemo.Consumer.EventConsumer;

internal class EventConsumer : IEventConsumer
{
    private readonly ILogger<EventConsumer> logger;
    private readonly EventProcessorClient eventProcessorClient;
    private readonly EventProcessorConfig eventProcessorConfig;

    public EventConsumer(
        ILogger<EventConsumer> logger,
        EventProcessorClient eventProcessorClient,
        IOptionsMonitor<EventProcessorConfig> optionsMonitor)
    {
        this.logger = logger;
        this.eventProcessorClient = eventProcessorClient;
        eventProcessorConfig = optionsMonitor.CurrentValue;
    }

    public async Task Consume(CancellationToken cancellationToken)
    {
        eventProcessorClient.ProcessEventAsync += (args) => ProcessEventHandler(args, cancellationToken);
        eventProcessorClient.ProcessErrorAsync += ProcessErrorHandler;

        await eventProcessorClient.StartProcessingAsync();
        //await Task.Delay(TimeSpan.FromSeconds(eventProcessorConfig.DelayInSeconds));
        if (cancellationToken.IsCancellationRequested)
        {
            await eventProcessorClient.StopProcessingAsync();
        }
    }

    private async Task ProcessEventHandler(ProcessEventArgs args, CancellationToken cancellationToken)
    {
        logger.LogInformation("Received event: {event} on partition: {partitionId}", 
            Encoding.UTF8.GetString(args.Data.EventBody),
            args.Partition.PartitionId);

        // Update checkpoint in blob storage
        await args.UpdateCheckpointAsync(cancellationToken);

        await Task.Delay(TimeSpan.FromSeconds(eventProcessorConfig.DelayInSeconds));
    }

    private Task ProcessErrorHandler(ProcessErrorEventArgs args)
    {
        logger.LogError("Partion {partitionId} encountered an unexpected exception {exceptionMessage}",
            args.PartitionId,
            args.Exception.Message);
        return Task.CompletedTask;
    }
}
