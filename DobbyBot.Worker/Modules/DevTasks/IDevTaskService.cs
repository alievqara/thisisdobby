namespace DobbyBot.Worker.Modules.DevTasks;

public interface IDevTaskService
{
    Task<string> HandleAsync(
        long telegramUserId,
        string message,
        CancellationToken cancellationToken);
}