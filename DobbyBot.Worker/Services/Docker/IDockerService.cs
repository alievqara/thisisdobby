namespace DobbyBot.Worker.Services.Docker;

public interface IDockerService
{
    Task<IReadOnlyList<DockerContainerGroup>> GetGroupsAsync(
        CancellationToken cancellationToken = default);

    Task<DockerContainerGroup?> GetGroupAsync(
        string groupKey,
        CancellationToken cancellationToken = default);

    Task<DockerContainerDetails?> GetContainerDetailsAsync(
        string containerId,
        CancellationToken cancellationToken = default);

    Task<string> GetContainerLogsAsync(
        string containerId,
        int tail = 80,
        CancellationToken cancellationToken = default);

    Task<string> GetContainerInspectSummaryAsync(
        string containerId,
        CancellationToken cancellationToken = default);
}