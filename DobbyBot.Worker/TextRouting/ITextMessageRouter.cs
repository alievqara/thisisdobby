namespace DobbyBot.Worker.TextRouting;

public interface ITextMessageRouter
{
    bool IsCommand(string text);

    Task<string> HandlePlainTextAsync(
        long telegramUserId,
        string text,
        CancellationToken cancellationToken);
}