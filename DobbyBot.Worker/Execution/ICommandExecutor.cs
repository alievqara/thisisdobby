namespace DobbyBot.Worker.Execution;

public interface ICommandExecutor
{
    Task<CommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);
}