namespace DobbyBot.Worker.State;

public sealed class BotUserState
{
    public BotUserMode Mode { get; set; } = BotUserMode.None;
    public string? PendingDownloaderInput { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}