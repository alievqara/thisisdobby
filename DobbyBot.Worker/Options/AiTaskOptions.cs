namespace DobbyBot.Worker.Options;

public sealed class AiTaskOptions
{
    public string PlanzyRepoPath { get; init; } = string.Empty;

    public string ClaudeCodeCommand { get; init; } = "claude";

    public int TaskTimeoutMinutes { get; init; } = 30;

    public int MaxOutputChars { get; init; } = 3500;
}