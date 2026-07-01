namespace DobbyBot.Worker.Options;

public sealed class LocalAiOptions
{
    public string BaseUrl { get; init; } = "http://127.0.0.1:11434";

    public string Model { get; init; } = "qwen3:1.7b";

    public int TimeoutSeconds { get; init; } = 120;

    public int MaxOutputTokens { get; init; } = 450;

    public double Temperature { get; init; } = 0.25;
}