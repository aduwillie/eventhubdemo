namespace EventHubDemo.Consolidated.Configuration;

internal class EventProcessorConfig
{
    public const string SectionName = "EventProcessor";
    public int DelayInSeconds { get; set; }
    public int TelemetryFlushTimeInSeconds { get; set; }
}
