using Azure.Identity;
using Azure.Messaging.EventHubs.Producer;
using EventHubDemo.Publisher.Application;
using EventHubDemo.Publisher.Configuration;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.EventCounterCollector;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var host = Host.CreateApplicationBuilder();

var applicationInsightConfig = new ApplicationInsightsConfig();
host.Configuration.Bind(ApplicationInsightsConfig.SectionName, applicationInsightConfig);

host.Services.AddApplicationInsightsTelemetryWorkerService(new ApplicationInsightsServiceOptions
{
    ConnectionString = applicationInsightConfig.ConnectionString,
    EnableAdaptiveSampling = true,
    EnableQuickPulseMetricStream = true,
});

host.Services.Configure<AzureEventHubConfig>(host.Configuration.GetSection(AzureEventHubConfig.SectionName));
host.Services.Configure<EventProducerConfig>(host.Configuration.GetSection(EventProducerConfig.SectionName));
host.Services.Configure<TelemetryConfiguration>(config =>
{
    var credential = new DefaultAzureCredential();
    config.SetAzureTokenCredential(credential);
});
host.Services.Configure<ApplicationInsightsConfig>(host.Configuration.GetSection(ApplicationInsightsConfig.SectionName));

host.Services.ConfigureTelemetryModule<EventCounterCollectionModule>((module, options) =>
{
    module.Counters.Clear();
    module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "cpu-usage"));
    module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "gc-heap-size"));
    module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "assembly-count"));
    module.Counters.Add(new EventCounterCollectionRequest("System.Net.Http", "requests-started"));
    module.Counters.Add(new EventCounterCollectionRequest("System.Net.Http", "requests-failed"));
});

host.Services.AddSingleton<IEventPublisher, EventPublisher>();
host.Services.AddSingleton((sp) =>
{
    var eventHubConfig = sp.GetRequiredService<IOptionsMonitor<AzureEventHubConfig>>().CurrentValue;

    return new EventHubProducerClient(
        fullyQualifiedNamespace: eventHubConfig?.FullyQualifiedNamespace,
        eventHubName: eventHubConfig?.HubName,
        credential: new DefaultAzureCredential());
});

var app = host.Build();

var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (sender, args) =>
{
    cancellationTokenSource.Cancel();
};

// Run the publisher
var eventPublisher = app.Services.GetRequiredService<IEventPublisher>();
await eventPublisher.Publish(cancellationTokenSource.Token);

await app.RunAsync();
