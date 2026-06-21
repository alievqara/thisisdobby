namespace DobbyBot.Worker.Execution;

public sealed record CommandResult(
    bool IsSuccess,
    string Output,
    string Error);