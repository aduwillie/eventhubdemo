namespace EventHubDemo.Consolidated.Configuration;

internal class AzureAppConfig
{
    public const string SectionName = "AzureAppConfig";
    public string Endpoint { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}
