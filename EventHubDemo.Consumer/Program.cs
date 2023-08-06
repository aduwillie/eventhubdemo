using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Storage.Blobs;
using EventHubDemo.Consumer.Configuration;
using EventHubDemo.Consumer.EventConsumer;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var host = Host.CreateApplicationBuilder(args);

var applicationInsightConfig = new ApplicationInsightsConfig();
host.Configuration.Bind(ApplicationInsightsConfig.SectionName, applicationInsightConfig);

host.Services.AddApplicationInsightsTelemetryWorkerService(new ApplicationInsightsServiceOptions
{
    ConnectionString = applicationInsightConfig.ConnectionString,
    EnableAdaptiveSampling = true,
    EnableQuickPulseMetricStream = true,
});

host.Services.Configure<BlobStorageConfig>(host.Configuration.GetSection(BlobStorageConfig.SectionName));
host.Services.Configure<AzureEventHubConfig>(host.Configuration.GetSection(AzureEventHubConfig.SectionName));
host.Services.Configure<EventProcessorConfig>(host.Configuration.GetSection(EventProcessorConfig.SectionName));
host.Services.Configure<ApplicationInsightsConfig>(host.Configuration.GetSection(ApplicationInsightsConfig.SectionName));

host.Services.AddSingleton<IEventConsumer, EventConsumer>();
host.Services.AddSingleton((sp) =>
{
    var blobStorageConfig = sp.GetRequiredService<IOptionsMonitor<BlobStorageConfig>>().CurrentValue;

    return new BlobContainerClient(
        blobContainerUri: new Uri(blobStorageConfig.Uri),
        credential: new DefaultAzureCredential());
});
host.Services.AddSingleton((sp) =>
{
    var eventHubConfig = sp.GetRequiredService<IOptionsMonitor<AzureEventHubConfig>>().CurrentValue;
    var blobContainerClient = sp.GetRequiredService<BlobContainerClient>();

    return new EventProcessorClient(
        checkpointStore: blobContainerClient,
        consumerGroup: EventHubConsumerClient.DefaultConsumerGroupName,
        fullyQualifiedNamespace: eventHubConfig.FullyQualifiedNamespace,
        eventHubName: eventHubConfig.HubName,
        credential: new DefaultAzureCredential());
});
host.Services.Configure<TelemetryConfiguration>(config =>
{
    var credential = new DefaultAzureCredential();
    config.SetAzureTokenCredential(credential);
});

var app = host.Build();

var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (sender, args) =>
{
    cancellationTokenSource.Cancel();
};

var eventConsumer = app.Services.GetRequiredService<IEventConsumer>();
await eventConsumer.Consume(cancellationToken: cancellationTokenSource.Token);

await app.RunAsync();
