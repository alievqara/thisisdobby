using System.Diagnostics;
using System.Text;

namespace DobbyBot.Worker.Services.Apps;

public interface IAppProcessRunner
{
    Task<string> RunAsync(string executable, string[] arguments, CancellationToken ct, string[]? alternate = null);
}

public sealed class AppProcessRunner : IAppProcessRunner
{
    public async Task<string> RunAsync(string executable, string[] arguments, CancellationToken ct, string[]? alternate = null)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true
            }
        };
        foreach (var arg in alternate ?? arguments) process.StartInfo.ArgumentList.Add(arg);
        process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        process.StartInfo.Environment["GCM_INTERACTIVE"] = "Never";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(executable == "git" || (alternate ?? arguments).Contains("build") || (alternate ?? arguments).Contains("up") || (alternate ?? arguments).Contains("pull") ? 15 : 1));
        process.Start();
        var stdout = ReadBoundedAsync(process.StandardOutput, timeout.Token);
        var stderr = ReadBoundedAsync(process.StandardError, timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch (InvalidOperationException) { }
            try { await Task.WhenAll(stdout, stderr); } catch (OperationCanceledException) { }
            if (ct.IsCancellationRequested) throw;
            throw new TimeoutException($"{executable}: vaxt limiti bitdi.");
        }
        var output = await stdout;
        var error = await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException($"{executable}: əməliyyat uğursuz oldu.\n{PanelText.Clip(error.Length > 0 ? error : output, 2000)}");
        // Docker logs writes application stderr separately; include both streams.
        var args = alternate ?? arguments;
        return executable == "docker" && args.Length > 0 && args[0] == "logs"
            ? (output + "\n" + error).Trim()
            : (string.IsNullOrWhiteSpace(output) ? error : output).Trim();
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken ct)
    {
        const int limit = 1024 * 1024;
        var text = new StringBuilder();
        var buffer = new char[8192];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            text.Append(buffer, 0, count);
            if (text.Length > limit) text.Remove(0, text.Length - limit);
        }
        return text.ToString();
    }
}
