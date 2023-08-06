namespace EventHubDemo.Consolidated.Configuration;

internal class WeatherApiConfig
{
    public const string SectionName = "WeatherApi";
    public string ApiKey { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
