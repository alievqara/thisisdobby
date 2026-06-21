namespace DobbyBot.Worker.State;

public interface IUserStateService
{
    BotUserState Get(long telegramUserId);
    void SetMode(long telegramUserId, BotUserMode mode);
    void SetPendingDownloaderInput(long telegramUserId, string input);
    void Clear(long telegramUserId);
}