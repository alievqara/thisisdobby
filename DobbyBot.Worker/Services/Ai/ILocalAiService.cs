namespace DobbyBot.Worker.Services.Ai;

public interface ILocalAiService
{
    Task<string> AskAsync(
        string question,
        CancellationToken cancellationToken = default);
}