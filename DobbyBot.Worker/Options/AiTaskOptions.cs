namespace DobbyBot.Worker.Options;

public sealed class AiTaskOptions
{
    public string PlanzyRepoPath { get; init; } = string.Empty;

    public string ClaudeCodeCommand { get; init; } = "claude";

    public int TaskTimeoutMinutes { get; init; } = 30;

    public int MaxOutputChars { get; init; } = 3500;

    public string Provider { get; init; } = "Ollama";

    public string OllamaBaseUrl { get; init; } = "http://127.0.0.1:11434";

    public string OllamaModel { get; init; } = "qwen3:1.7b";

    public int OllamaTimeoutSeconds { get; init; } = 120;

    public int OllamaMaxOutputTokens { get; init; } = 450;

    public double OllamaTemperature { get; init; } = 0.25;
}