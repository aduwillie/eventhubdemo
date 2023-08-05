namespace EventHubDemo.Consumer.Configuration;

internal class EventProcessorConfig
{
    public const string SectionName = "EventProcessor";
    public int DelayInSeconds { get; set; }
}
