using Azure.Messaging.EventHubs.Producer;
using Microsoft.ApplicationInsights;
using System.Text.Json;

namespace EventHubDemo.Consolidated;

internal class PublisherWorker : BackgroundService
{
    private readonly ILogger<PublisherWorker> logger;
    private readonly EventHubProducerClient eventHubProducerClient;
    private readonly TelemetryClient telemetryClient;
    private Timer? timer;

    private readonly Object lockToken = new Object();
    private readonly List<string> eventList = new List<string>();

    public PublisherWorker(
        ILogger<PublisherWorker> logger,
        EventHubProducerClient eventHubProducerClient,
        TelemetryClient telemetryClient)
    {
        this.logger = logger;
        this.eventHubProducerClient = eventHubProducerClient;
        this.telemetryClient = telemetryClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        timer = new Timer(
            callback: _ =>
            {
                _ = ProcessBatch(stoppingToken);
            },
            state: null,
            dueTime: 5000,
            period: 15000);

        foreach (var data in GetEvent())
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            logger.LogInformation("Publisher worker running at: {time}", DateTimeOffset.Now);
            logger.LogInformation(data);
            lock (lockToken)
            {
                eventList.Add(data);
            }

            await Task.Delay(5000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        await telemetryClient.FlushAsync(cancellationToken);
        timer?.DisposeAsync();
        await eventHubProducerClient.DisposeAsync();
    }

    private IEnumerable<string> GetEvent()
    {
        while (true)
        {
            var randomGenerator = new Random();
            var eventData = new Models.EventData(randomGenerator.Next(1, 1000));

            yield return JsonSerializer.Serialize(eventData);
        }
    }

    private async Task ProcessBatch(CancellationToken cancellationToken)
    {
        var eventListCopy = new string[eventList.Count * 2];
        Array.Fill(eventListCopy, string.Empty);

        lock (lockToken)
        {
            eventList.CopyTo(eventListCopy);
            eventList.Clear();
        }

        eventListCopy = eventListCopy.Where(x => !string.IsNullOrEmpty(x)).ToArray();
        var processedEvents = new List<int>();

        if (eventListCopy.Length == 0)
        {
            logger.LogInformation("No batch to process at this time {time}", DateTimeOffset.Now);
            return;
        }

        logger.LogInformation("Processing batch events of size {batchSize}", eventListCopy.Length);
        var batch = await eventHubProducerClient.CreateBatchAsync(cancellationToken);
        foreach (var data in eventListCopy)
        {
            if (!batch.TryAdd(new(data)))
            {
                logger.LogInformation("Unable to process event {eventData}", data);
            }
            else
            {
                var deserializedValue = JsonSerializer.Deserialize<Models.EventData>(data);
                processedEvents.Add(deserializedValue!.Value);
            }
        }

        await eventHubProducerClient.SendAsync(batch, cancellationToken);
        logger.LogInformation("Sent {batchSize} to EventHub - {Values}",
            processedEvents.Count,
            string.Join(",", processedEvents));
    }
}