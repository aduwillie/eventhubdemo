namespace EventHubDemo.Consolidated.Configuration;

internal class ApplicationInsightsConfig
{
    public const string SectionName = "ApplicationInsights";
    public string ConnectionString { get; set; } = string.Empty;
}
