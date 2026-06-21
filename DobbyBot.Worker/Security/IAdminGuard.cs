namespace DobbyBot.Worker.Security;

public interface IAdminGuard
{
    bool IsAdmin(long telegramUserId);
}