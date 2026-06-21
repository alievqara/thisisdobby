using System.Text;
using DobbyBot.Worker.Execution;

namespace DobbyBot.Worker.Services;

public sealed class ServerStatusService : IServerStatusService
{
    private readonly ICommandExecutor _executor;

    public ServerStatusService(ICommandExecutor executor)
    {
        _executor = executor;
    }

    public async Task<string> GetStatusAsync(CancellationToken cancellationToken)
    {
        var hostname = await _executor.RunAsync("hostname", [], cancellationToken);
        var uptime = await _executor.RunAsync("uptime", ["-p"], cancellationToken);
        var memory = await _executor.RunAsync("free", ["-h"], cancellationToken);
        var disk = await _executor.RunAsync("df", ["-h", "/"], cancellationToken);

        var result = new StringBuilder();

        result.AppendLine("🧦 Dobby Server Status");
        result.AppendLine();
        result.AppendLine($"Host: {GetOutput(hostname)}");
        result.AppendLine($"Uptime: {GetOutput(uptime)}");
        result.AppendLine();
        result.AppendLine("Memory:");
        result.AppendLine(GetOutput(memory));
        result.AppendLine();
        result.AppendLine("Disk:");
        result.AppendLine(GetOutput(disk));

        return result.ToString();
    }

    private static string GetOutput(CommandResult result)
    {
        if (result.IsSuccess)
        {
            return result.Output;
        }

        return string.IsNullOrWhiteSpace(result.Error)
            ? "unknown"
            : result.Error;
    }
}