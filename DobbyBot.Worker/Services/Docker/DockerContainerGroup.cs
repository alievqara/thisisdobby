namespace DobbyBot.Worker.Services.Docker;

public sealed record DockerContainerGroup(
    string Key,
    string Icon,
    string Title,
    IReadOnlyList<DockerContainerInfo> Containers)
{
    public int Count => Containers.Count;
}