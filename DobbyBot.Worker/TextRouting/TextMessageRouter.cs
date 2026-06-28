using DobbyBot.Worker.Modules.DevTasks;

namespace DobbyBot.Worker.TextRouting;

public sealed class TextMessageRouter : ITextMessageRouter
{
    private readonly IDevTaskService _devTaskService;

    public TextMessageRouter(IDevTaskService devTaskService)
    {
        _devTaskService = devTaskService;
    }

    public bool IsCommand(string text)
    {
        return text.TrimStart().StartsWith('/');
    }

    public Task<string> HandlePlainTextAsync(
        long telegramUserId,
        string text,
        CancellationToken cancellationToken)
    {
        return _devTaskService.HandleAsync(
            telegramUserId,
            text.Trim(),
            cancellationToken);
    }
}