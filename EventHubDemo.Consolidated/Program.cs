using EventHubDemo.Consolidated;
using EventHubDemo.Consolidated.Configuration;
using EventHubDemo.Consolidated.Services;

var applicationBuilder = Host.CreateApplicationBuilder(args);

applicationBuilder.Services.Configure<ApplicationInsightsConfig>(applicationBuilder.Configuration.GetSection(ApplicationInsightsConfig.SectionName));
applicationBuilder.Services.Configure<AzureAppConfig>(applicationBuilder.Configuration.GetSection(AzureAppConfig.SectionName));
applicationBuilder.Services.Configure<AzureEventHubConfig>(applicationBuilder.Configuration.GetSection(AzureEventHubConfig.SectionName));
applicationBuilder.Services.Configure<BlobStorageConfig>(applicationBuilder.Configuration.GetSection(BlobStorageConfig.SectionName));
applicationBuilder.Services.Configure<EventProcessorConfig>(applicationBuilder.Configuration.GetSection(EventProcessorConfig.SectionName));
applicationBuilder.Services.Configure<WeatherApiConfig>(applicationBuilder.Configuration.GetSection(WeatherApiConfig.SectionName));

applicationBuilder.Services.AddHostedService<PublisherWorker>();
applicationBuilder.Services.AddHostedService<ConsumerWorker>();

applicationBuilder
    .AddBlobStorage()
    .AddEventHub()
    .AddApplicationInsights()
    .AddAzureAppConfiguration()
    .AddWeatherServiceApi();

var host = applicationBuilder.Build();

host.Run();
