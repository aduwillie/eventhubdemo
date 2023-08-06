namespace EventHubDemo.Consolidated.Services
{
    internal interface IWeatherService
    {
        Task<string> GetAccraWeather(CancellationToken cancellationToken = default);
    }
}