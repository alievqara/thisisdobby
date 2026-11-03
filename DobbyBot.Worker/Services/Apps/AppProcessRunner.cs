using System.Diagnostics;
namespace DobbyBot.Worker.Services.Apps;

public interface IAppProcessRunner
{
    Task<string> RunAsync(string executable, string[] arguments, CancellationToken ct, string[]? alternate = null);
}

public sealed class AppProcessRunner : IAppProcessRunner
{
    public async Task<string> RunAsync(string executable, string[] arguments, CancellationToken ct, string[]? alternate = null)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
        foreach (var arg in alternate ?? arguments) process.StartInfo.ArgumentList.Add(arg);
        process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { try { process.Kill(true); } catch { } throw; }
        var output = await stdout;
        var error = await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException($"{executable}: əməliyyat uğursuz oldu.\n{error}");
        return (string.IsNullOrWhiteSpace(output) ? error : output).Trim();
    }
}


