namespace DobbyBot.Worker.Services.Docker;

public sealed record DockerContainerInfo(
    string Id,
    string Name,
    string Image,
    string Status,
    string State,
    string? Health,
    string Uptime,
    string GroupKey);