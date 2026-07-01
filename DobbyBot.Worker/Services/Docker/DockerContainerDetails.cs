namespace DobbyBot.Worker.Services.Docker;

public sealed record DockerContainerDetails(
    DockerContainerInfo Container,
    DockerContainerStats? Stats,
    string RestartPolicy,
    string NetworkSummary,
    string MountsSummary,
    string CreatedAt);