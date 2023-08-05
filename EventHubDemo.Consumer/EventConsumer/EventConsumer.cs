using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using EventHubDemo.Consumer.Configuration;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
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
    private readonly System.Timers.Timer telemetryFlushTimer;

    private int eventsConsumed = 0;

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

        telemetryFlushTimer = new System.Timers.Timer(eventProcessorConfig.TelemetryFlushTimeInSeconds);
        telemetryFlushTimer.Elapsed += async (_, __) => await HandleMetricFlushing();
    }

    public async Task Consume(CancellationToken cancellationToken)
    {
        eventProcessorClient.ProcessEventAsync += (args) => ProcessEventHandler(args, cancellationToken);
        eventProcessorClient.ProcessErrorAsync += ProcessErrorHandler;

        await eventProcessorClient.StartProcessingAsync();
        telemetryFlushTimer.Start();
        telemetryClient.TrackEvent(TELEMETRY_EVENT_PROCESSOR_STARTED);

        if (cancellationToken.IsCancellationRequested)
        {
            await eventProcessorClient.StopProcessingAsync();
            telemetryFlushTimer.Stop();
            telemetryFlushTimer.Dispose();
            telemetryClient.TrackEvent(TELEMETRY_EVENT_PROCESSOR_STOPPED);
        }
    }

    private async Task ProcessEventHandler(ProcessEventArgs args, CancellationToken cancellationToken)
    {
        logger.LogInformation("Received event: {event} on partition: {partitionId}",
            Encoding.UTF8.GetString(args.Data.EventBody),
            args.Partition.PartitionId);

        Interlocked.Increment(ref eventsConsumed);

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

        await Task.Delay(TimeSpan.FromSeconds(eventProcessorConfig.DelayInSeconds));
    }

    private Task ProcessErrorHandler(ProcessErrorEventArgs args)
    {
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

        return Task.CompletedTask;
    }

    private Task HandleMetricFlushing()
    {
        if (eventsConsumed is not 0)
        {
            telemetryClient.TrackMetric("EventsConsumed", eventsConsumed);
        }

        return Task.CompletedTask;
    }
}
