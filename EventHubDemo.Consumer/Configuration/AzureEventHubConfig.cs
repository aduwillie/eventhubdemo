namespace EventHubDemo.Consumer.Configuration;

internal class AzureEventHubConfig
{
    public const string SectionName = "AzureEventHub";
    public string FullyQualifiedNamespace { get; set; } = string.Empty;
    public string HubName { get; set; } = string.Empty;
}
