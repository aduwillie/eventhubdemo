using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Producer;
using Azure.Storage.Blobs;
using EventHubDemo.Consolidated.Configuration;
using EventHubDemo.Consolidated.Services;
using Microsoft.ApplicationInsights.Extensibility.EventCounterCollector;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Extensions.Options;
using Polly;

namespace EventHubDemo.Consolidated;

internal static class DependencyInjection
{
    public static HostApplicationBuilder AddBlobStorage(this HostApplicationBuilder builder)
    {
        builder.Services.AddSingleton(sp =>
        {
            var blobStorageConfig = sp.GetRequiredService<IOptionsMonitor<BlobStorageConfig>>().CurrentValue;

            return new BlobContainerClient(
                blobContainerUri: new(blobStorageConfig.Uri),
                credential: new DefaultAzureCredential());
        });

        return builder;
    }

    public static HostApplicationBuilder AddEventHub(this HostApplicationBuilder builder)
    {
        builder.Services.AddSingleton(provider =>
        {
            var eventHubConfig = provider.GetRequiredService<IOptionsMonitor<AzureEventHubConfig>>().CurrentValue;

            return new EventHubProducerClient(
                fullyQualifiedNamespace: eventHubConfig.FullyQualifiedNamespace,
                eventHubName: eventHubConfig.HubName,
                credential: new DefaultAzureCredential(),
                clientOptions: new EventHubProducerClientOptions
                {
                    RetryOptions = new Azure.Messaging.EventHubs.EventHubsRetryOptions
                    {
                        MaximumRetries = 5,
                        MaximumDelay = TimeSpan.FromSeconds(5),
                        Mode = Azure.Messaging.EventHubs.EventHubsRetryMode.Exponential,
                    }
                });
        });

        builder.Services.AddSingleton(provider =>
        {
            var eventHubConfig = provider.GetRequiredService<IOptionsMonitor<AzureEventHubConfig>>().CurrentValue;
            var blobContainerClient = provider.GetRequiredService<BlobContainerClient>();

            return new EventProcessorClient(
                checkpointStore: blobContainerClient,
                consumerGroup: EventHubConsumerClient.DefaultConsumerGroupName,
                fullyQualifiedNamespace: eventHubConfig.FullyQualifiedNamespace,
                eventHubName: eventHubConfig.HubName,
                credential: new DefaultAzureCredential());
        });

        return builder;
    }

    public static HostApplicationBuilder AddApplicationInsights(this HostApplicationBuilder builder)
    {
        var applicationInsightsConfig = new ApplicationInsightsConfig();
        builder.Configuration.Bind(ApplicationInsightsConfig.SectionName, applicationInsightsConfig);

        builder.Services.AddApplicationInsightsTelemetryWorkerService(new ApplicationInsightsServiceOptions
        {
            ConnectionString = applicationInsightsConfig.ConnectionString,
        });

        builder.Services.ConfigureTelemetryModule<EventCounterCollectionModule>((module, options) =>
        {
            module.Counters.Clear();
            module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "cpu-usage"));
            module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "gc-heap-size"));
            module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "assembly-count"));
            module.Counters.Add(new EventCounterCollectionRequest("System.Net.Http", "requests-started"));
            module.Counters.Add(new EventCounterCollectionRequest("System.Net.Http", "requests-failed"));
        });

        return builder;
    }

    public static HostApplicationBuilder AddAzureAppConfiguration(this HostApplicationBuilder builder)
    {
        var appConfigSettings = new AzureAppConfig();
        builder.Configuration.Bind(AzureAppConfig.SectionName, appConfigSettings);

        builder.Configuration.AddAzureAppConfiguration(options =>
        {
            options.Connect(
                endpoint: new Uri(appConfigSettings.Endpoint),
                credential: new DefaultAzureCredential());
        });

        // Add to DI, configures the refresher provider
        builder.Services.AddAzureAppConfiguration();

        return builder;
    }

    public static HostApplicationBuilder AddWeatherServiceApi(this HostApplicationBuilder builder)
    {
        // Illustrating retry logic and policies
        builder.Services
            .AddHttpClient<IWeatherService, WeatherService>("weatherapi", (sp, client) =>
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


        return builder;
    }
}
