using DobbyBot.Worker.Options;
using Microsoft.Extensions.Options;

namespace DobbyBot.Worker.Security;

public sealed class AdminGuard : IAdminGuard
{
    private readonly DobbyBotOptions _options;

    public AdminGuard(IOptions<DobbyBotOptions> options)
    {
        _options = options.Value;
    }

    public bool IsAdmin(long telegramUserId)
    {
        return telegramUserId == _options.AdminTelegramId;
    }
}