namespace DobbyBot.Worker.Services.Docker;

public sealed record DockerContainerStats(
    string Cpu,
    string Memory,
    string MemoryPercent,
    string Network,
    string BlockIo);