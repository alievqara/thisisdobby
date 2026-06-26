using System.Collections.Concurrent;

namespace DobbyBot.Worker.State;

public sealed class InMemoryUserStateService : IUserStateService
{
    private readonly ConcurrentDictionary<long, BotUserState> _states = new();

    public BotUserState Get(long telegramUserId)
    {
        return _states.GetOrAdd(telegramUserId, _ => new BotUserState());
    }

    public void SetMode(long telegramUserId, BotUserMode mode)
    {
        var state = Get(telegramUserId);

        state.Mode = mode;
        state.UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetPendingDownloaderInput(long telegramUserId, string input)
    {
        var state = Get(telegramUserId);

        state.PendingDownloaderInput = input;
        state.UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ClearMenuMessageId(long telegramUserId)
    {
        var state = Get(telegramUserId);

        state.MenuMessageId = null;
        state.UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetMenuMessageId(long telegramUserId, long messageId)
    {
        var state = Get(telegramUserId);

        state.MenuMessageId = messageId;
        state.UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Clear(long telegramUserId)
    {
        var state = Get(telegramUserId);

        state.Mode = BotUserMode.None;
        state.PendingDownloaderInput = null;
        state.UpdatedAtUtc = DateTime.UtcNow;

        // Vacib:
        // MenuMessageId-ni silmirik.
        // Çünki /start gələndə köhnə menu mesajını edit etmək istəyirik.
    }
}