namespace DobbyBot.Worker.Modules.DevTasks;

public sealed record DevTaskRequest(
    long TelegramUserId,
    string Message);