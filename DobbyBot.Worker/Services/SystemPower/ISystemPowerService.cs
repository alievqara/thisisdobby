namespace DobbyBot.Worker.Services.SystemPower;

public interface ISystemPowerService
{
    Task<string> GetRuntimeInfoAsync(CancellationToken cancellationToken = default);

    Task<string> GetDobbyLogsAsync(CancellationToken cancellationToken = default);

    Task<string> RebootAsync(CancellationToken cancellationToken = default);

    Task<string> PowerOffAsync(CancellationToken cancellationToken = default);
}