namespace DobbyBot.Worker.Services;

public interface IServerStatusService
{
    Task<string> GetStatusAsync(CancellationToken cancellationToken);
}