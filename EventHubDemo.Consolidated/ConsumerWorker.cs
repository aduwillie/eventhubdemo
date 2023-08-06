using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using EventHubDemo.Consolidated.Services;
using Microsoft.ApplicationInsights;
using System.Text;

namespace EventHubDemo.Consolidated;

internal class ConsumerWorker : BackgroundService
{
    private readonly ILogger<ConsumerWorker> logger;
    private readonly EventProcessorClient eventProcessorClient;
    private readonly TelemetryClient telemetryClient;
    private readonly IServiceProvider serviceProvider;

    public ConsumerWorker(
        ILogger<ConsumerWorker> logger,
        EventProcessorClient eventProcessorClient,
        TelemetryClient telemetryClient,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.eventProcessorClient = eventProcessorClient;
        this.telemetryClient = telemetryClient;
        this.serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Consumer worker running at: {time}", DateTimeOffset.Now);

        eventProcessorClient.ProcessEventAsync += ProcessEventHandler;
        eventProcessorClient.ProcessErrorAsync += ProcessErrorHandler;

        await eventProcessorClient.StartProcessingAsync();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await eventProcessorClient.StopProcessingAsync();
    }

    private async Task ProcessEventHandler(ProcessEventArgs args)
    {
        logger.LogInformation("Received event: {event} on partition: {partitionId}",
           Encoding.UTF8.GetString(args.Data.EventBody),
           args.Partition.PartitionId);

        // Make fake http call
        var weatherApi = serviceProvider.GetRequiredService<IWeatherService>();
        var weatherForAccra = await weatherApi.GetAccraWeather(args.CancellationToken);
        logger.LogInformation("Weather from Accra: {accraWeather}", weatherForAccra);

        // Commit changes to blob storage via Checkpointing 
        await args.UpdateCheckpointAsync(args.CancellationToken);
    }

    private Task ProcessErrorHandler(ProcessErrorEventArgs args)
    {
        logger.LogError("Partion {partitionId} encountered an unexpected exception {exceptionMessage}",
            args.PartitionId,
            args.Exception.Message);

        return Task.CompletedTask;
    }
}
