using Azure.Core;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Storage.Blobs;
using EventHubDemo.Consumer.Configuration;
using EventHubDemo.Consumer.EventConsumer;
using EventHubDemo.Consumer.Services;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.EventCounterCollector;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Polly;

var host = Host.CreateApplicationBuilder(args);

var applicationInsightConfig = new ApplicationInsightsConfig();
host.Configuration.Bind(ApplicationInsightsConfig.SectionName, applicationInsightConfig);

host.Services.AddApplicationInsightsTelemetryWorkerService(new ApplicationInsightsServiceOptions
{
    ConnectionString = applicationInsightConfig.ConnectionString,
    EnableAdaptiveSampling = true,
    EnableQuickPulseMetricStream = true,
});

// Add Azure App Configuration to DI
var azureAppConfig = new AzureAppConfig();
host.Configuration.Bind(AzureAppConfig.SectionName, azureAppConfig);

host.Configuration.AddAzureAppConfiguration(options =>
{
    options.Connect(endpoint: new(azureAppConfig.Endpoint), credential: new DefaultAzureCredential());
    //options.ConfigureRefresh(refresh =>
    //{
    //    refresh.SetCacheExpiration(TimeSpan.FromMinutes(1)); // Refresh app configuration keys every 1min
    //});
});
host.Services.AddAzureAppConfiguration();

host.Services.Configure<BlobStorageConfig>(host.Configuration.GetSection(BlobStorageConfig.SectionName));
host.Services.Configure<AzureEventHubConfig>(host.Configuration.GetSection(AzureEventHubConfig.SectionName));
host.Services.Configure<EventProcessorConfig>(host.Configuration.GetSection(EventProcessorConfig.SectionName));
host.Services.Configure<ApplicationInsightsConfig>(host.Configuration.GetSection(ApplicationInsightsConfig.SectionName));
host.Services.Configure<WeatherApiConfig>(host.Configuration.GetSection(WeatherApiConfig.SectionName));

host.Services.ConfigureTelemetryModule<EventCounterCollectionModule>((module, options) =>
{
    module.Counters.Clear();
    module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "cpu-usage"));
    module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "gc-heap-size"));
    module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "assembly-count"));
    module.Counters.Add(new EventCounterCollectionRequest("System.Net.Http", "requests-started"));
    module.Counters.Add(new EventCounterCollectionRequest("System.Net.Http", "requests-failed"));
});

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

// Illustrating retry logic and policies
host.Services
    .AddHttpClient<IWeatherService, WeatherService>("weatherapi" ,(sp, client) =>
    {
        var config = sp.GetRequiredService<IOptionsMonitor<WeatherApiConfig>>().CurrentValue;

        client.BaseAddress = new(config.Url);
    })
    // Handle retries, up to 3, with 5s intervals
    .AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: _ => TimeSpan.FromSeconds(5)))
    // Circuit breaker config, 5 issues with endpoint, cooldown of 5s
    .AddTransientHttpErrorPolicy(policy => policy.CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(5)))
    // Add a 5s request timeout for all GET requests
    .AddPolicyHandler(request =>
    {
        if (request.Method == HttpMethod.Get)
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(5));
        }

        return Policy.NoOpAsync<HttpResponseMessage>();
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
