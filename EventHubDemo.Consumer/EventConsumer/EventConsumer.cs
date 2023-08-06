using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using EventHubDemo.Consumer.Configuration;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace EventHubDemo.Consumer.EventConsumer;

internal class EventConsumer : IEventConsumer
{
    private const string TELEMETRY_EVENT_PROCESSOR_STARTED = "Event processor started successfully";
    private const string TELEMETRY_EVENT_PROCESSOR_STOPPED = "Event processor stopped";
    private const string TELEMETRY_CHECKPOINT_UPDATED = "Checkpoint updated";

    private readonly ILogger<EventConsumer> logger;
    private readonly EventProcessorClient eventProcessorClient;
    private readonly EventProcessorConfig eventProcessorConfig;
    private readonly TelemetryClient telemetryClient;
    private Timer? timer;

    private long eventsConsumed = 0;

    public EventConsumer(
        ILogger<EventConsumer> logger,
        EventProcessorClient eventProcessorClient,
        IOptionsMonitor<EventProcessorConfig> optionsMonitor,
        TelemetryClient telemetryClient)
    {
        this.logger = logger;
        this.eventProcessorClient = eventProcessorClient;
        eventProcessorConfig = optionsMonitor.CurrentValue;
        this.telemetryClient = telemetryClient;
    }

    public async Task Consume(CancellationToken cancellationToken)
    {
        eventProcessorClient.ProcessEventAsync += (args) => ProcessEventHandler(args, cancellationToken);
        eventProcessorClient.ProcessErrorAsync += (args) => ProcessErrorHandler(args, cancellationToken);

        await eventProcessorClient.StartProcessingAsync();
        telemetryClient.TrackEvent(TELEMETRY_EVENT_PROCESSOR_STARTED);

        timer = new Timer(
            callback: (_) =>
            {
                _ = HandleMetricFlushing();
            },
            state: null,
            dueTime: 1000,
            period: 2000); // 5 seconds interval
    }

    private async Task ProcessEventHandler(ProcessEventArgs args, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            await eventProcessorClient.StopProcessingAsync();
            telemetryClient.TrackEvent(TELEMETRY_EVENT_PROCESSOR_STOPPED);
            return;
        }

        logger.LogInformation("Received event: {event} on partition: {partitionId}",
            Encoding.UTF8.GetString(args.Data.EventBody),
            args.Partition.PartitionId);

        eventsConsumed += 1;

        // Track successful operation event in AI
        telemetryClient.TrackEvent(
            eventName: "Data processed successfully",
            properties: new Dictionary<string, string>
            {
                { "PartitionId", args.Partition.PartitionId },
                { "ConsumerGroup", args.Partition.ConsumerGroup },
                { "MessageId", args.Data.MessageId },
                { "SequenceNumber", args.Data.SequenceNumber.ToString() },
            });

        // Update checkpoint in blob storage
        await args.UpdateCheckpointAsync(cancellationToken);
        telemetryClient.TrackEvent(TELEMETRY_CHECKPOINT_UPDATED);
    }

    private async Task ProcessErrorHandler(ProcessErrorEventArgs args, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            await eventProcessorClient.StopProcessingAsync();
            telemetryClient.TrackEvent(TELEMETRY_EVENT_PROCESSOR_STOPPED);
            return;
        }

        logger.LogError("Partion {partitionId} encountered an unexpected exception {exceptionMessage}",
            args.PartitionId,
            args.Exception.Message);
        telemetryClient.TrackEvent(
            eventName: "Processed error event",
            properties: new Dictionary<string, string>
            {
                { "PartitionId", args.PartitionId },
                { "ErrorMessage", args.Exception.Message },
            });
    }

    private Task HandleMetricFlushing()
    {
        if (eventsConsumed != 0)
        {
            telemetryClient.TrackMetric("EventsConsumed", eventsConsumed);
            eventsConsumed = 0;
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        timer?.Dispose();
        return ValueTask.CompletedTask;
    }
}
