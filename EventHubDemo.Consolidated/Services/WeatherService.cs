using EventHubDemo.Consolidated.Configuration;
using Microsoft.Extensions.Options;

namespace EventHubDemo.Consolidated.Services;

internal class WeatherService : IWeatherService
{
    private readonly HttpClient httpClient;
    private readonly WeatherApiConfig weatherApiConfig;

    public WeatherService(HttpClient httpClient, IOptionsMonitor<WeatherApiConfig> optionsMonitor)
    {
        this.httpClient = httpClient;
        weatherApiConfig = optionsMonitor.CurrentValue;
    }

    public async Task<string> GetAccraWeather(CancellationToken cancellationToken = default)
    {
        var q = $"?key={weatherApiConfig.ApiKey}&q={weatherApiConfig.CityName}";
        var response = await httpClient.GetAsync(q);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}
