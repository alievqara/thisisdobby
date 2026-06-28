namespace DobbyBot.Worker.Modules.DevTasks;

public sealed record DevTaskResult(
    bool IsSuccess,
    string Summary,
    string Output,
    string Error,
    TimeSpan Duration);