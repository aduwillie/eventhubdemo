using Azure.Identity;
using Azure.Messaging.EventHubs.Producer;
using EventHubDemo.Publisher.Application;
using EventHubDemo.Publisher.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var host = Host.CreateApplicationBuilder();

host.Services.Configure<AzureEventHubConfig>(host.Configuration.GetSection(AzureEventHubConfig.SectionName));
host.Services.Configure<EventProducerConfig>(host.Configuration.GetSection(EventProducerConfig.SectionName));

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

// Run the publisher
var eventPublisher = app.Services.GetRequiredService<IEventPublisher>();
await eventPublisher.Publish();

//await app.RunAsync();
