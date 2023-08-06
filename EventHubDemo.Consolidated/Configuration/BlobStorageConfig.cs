namespace EventHubDemo.Consolidated.Configuration;

internal class BlobStorageConfig
{
    public const string SectionName = "BlobStorage";
    public string Uri { get; set; } = string.Empty;
}
