using Azure.Messaging.EventHubs.Producer;
using EventHubDemo.Publisher.Configuration;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace EventHubDemo.Publisher.Application;

internal class EventPublisher : IEventPublisher
{
    private const string TELEMETRY_OPERATION_NAME = "Publishing events";
    private const string TELEMETRY_BATCH_ADDED_SUCCESSFULLY = "Batch events published successfully";
    private const string TELEMETRY_ADDING_EVENT_BATCH_FAILED = "Adding event failed";

    private readonly ILogger<EventPublisher> logger;
    private readonly EventHubProducerClient eventHubProducerClient;
    private readonly EventProducerConfig eventProducerConfig;
    private readonly TelemetryClient telemetryClient;
    private Timer? timer;

    public EventPublisher(
        ILogger<EventPublisher> logger,
        EventHubProducerClient eventHubProducerClient,
        IOptionsMonitor<EventProducerConfig> optionsMonitor,
        TelemetryClient telemetryClient)
    {
        this.logger = logger;
        this.eventHubProducerClient = eventHubProducerClient;
        this.eventProducerConfig = optionsMonitor.CurrentValue;
        this.telemetryClient = telemetryClient;
    }

    public Task Publish(CancellationToken cancellationToken = default)
    {
        timer = new Timer(
            callback: (_) =>
            {
                _ = DoWork(cancellationToken);
            },
            state: null,
            dueTime: 1000,
            period: 10000); // 10 seconds interval

        return Task.CompletedTask;
    }

    private async Task DoWork(CancellationToken cancellationToken)
    {
        logger.LogInformation("Publishing events...");

        using (telemetryClient.StartOperation<RequestTelemetry>(TELEMETRY_OPERATION_NAME))
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var batch = await GenerateBatchEvents(eventProducerConfig.Count, cancellationToken);
                await eventHubProducerClient.SendAsync(batch, cancellationToken);

                logger.LogInformation("A batch of {totalEvents} total events sent", batch.Count);
                telemetryClient.TrackEvent(
                    eventName: TELEMETRY_BATCH_ADDED_SUCCESSFULLY,
                    properties: null,
                    metrics: new Dictionary<string, double>
                    {
                        { "Count", batch.Count }
                    });

                await Task.Delay(TimeSpan.FromSeconds(10)); // wait 5 seconds
            }
        }
    }

    private async Task<EventDataBatch> GenerateBatchEvents(int batchSize, CancellationToken cancellationToken)
    {
        var eventDataBatch = await eventHubProducerClient.CreateBatchAsync(cancellationToken);

        for (var i = 0; i < batchSize; i++)
        {
            if (cancellationToken.IsCancellationRequested || batchSize == 0)
            {
                return eventDataBatch;
            }

            var randomGen = new Random();
            var eventData = $"Event: {randomGen.Next(1, 1000)}";

            if (!eventDataBatch.TryAdd(new(Encoding.UTF8.GetBytes(eventData))))
            {
                logger.LogError($"Event {i} is too large for the batch and cannot be sent.");
                telemetryClient.TrackEvent(
                    eventName: TELEMETRY_ADDING_EVENT_BATCH_FAILED,
                    properties: new Dictionary<string, string>
                    {
                        { "EVent", eventData },
                    });
            }
        }

        return eventDataBatch;
    }
}
