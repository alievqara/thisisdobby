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

    public void Clear(long telegramUserId)
    {
        _states.TryRemove(telegramUserId, out _);
    }
}