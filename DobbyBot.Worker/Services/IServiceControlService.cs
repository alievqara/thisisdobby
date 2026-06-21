namespace DobbyBot.Worker.Services;

public interface IServiceControlService
{
    Task<string> GetServicesAsync(CancellationToken cancellationToken);

    Task<string> RestartAsync(
        string serviceKey,
        CancellationToken cancellationToken);

    Task<string> LogsAsync(
        string serviceKey,
        CancellationToken cancellationToken);
}