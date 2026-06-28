namespace DobbyBot.Worker.Modules.DevTasks;

public interface IAgentRunnerService
{
    Task<DevTaskResult> RunAsync(
        DevTaskRequest request,
        CancellationToken cancellationToken);
}