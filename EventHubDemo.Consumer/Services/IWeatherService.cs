namespace EventHubDemo.Consumer.Services
{
    internal interface IWeatherService
    {
        Task<string> GetAccraWeather(CancellationToken cancellationToken = default);
    }
}