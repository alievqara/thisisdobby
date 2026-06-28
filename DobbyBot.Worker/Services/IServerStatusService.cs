namespace DobbyBot.Worker.Services;

public interface IServerStatusService
{
    Task<string> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<string> GetMemoryStatusAsync(CancellationToken cancellationToken = default);

    Task<string> GetDiskStatusAsync(CancellationToken cancellationToken = default);

    Task<string> GetLoadStatusAsync(CancellationToken cancellationToken = default);

    Task<string> GetFailedServicesAsync(CancellationToken cancellationToken = default);

    Task<string> GetAllContainersAsync(CancellationToken cancellationToken = default);

    Task<string> GetRunningContainersAsync(CancellationToken cancellationToken = default);

    Task<string> GetFailedContainersAsync(CancellationToken cancellationToken = default);

    Task<string> GetContainerLogsSummaryAsync(CancellationToken cancellationToken = default);

    Task<string> GetPostgresStatusAsync(CancellationToken cancellationToken = default);

    Task<string> GetRedisStatusAsync(CancellationToken cancellationToken = default);

    Task<string> GetVolumesStatusAsync(CancellationToken cancellationToken = default);

    Task<string> GetDbBackupsStatusAsync(CancellationToken cancellationToken = default);

    Task<string> GetProxyStatusAsync(CancellationToken cancellationToken = default);

    Task<string> GetSslStatusAsync(CancellationToken cancellationToken = default);

    Task<string> GetDockerStatusAsync(CancellationToken cancellationToken = default);
}