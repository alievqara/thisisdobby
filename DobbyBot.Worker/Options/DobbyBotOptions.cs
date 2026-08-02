namespace DobbyBot.Worker.Options;

public sealed class DobbyBotOptions
{
    public const string SectionName = "DobbyBot";

    public string Token { get; init; } = string.Empty;

    public long AdminTelegramId { get; init; }
}