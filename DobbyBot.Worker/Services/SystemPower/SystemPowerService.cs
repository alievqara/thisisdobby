using System.Diagnostics;

namespace DobbyBot.Worker.Services.SystemPower;

public sealed class SystemPowerService : ISystemPowerService
{
    public async Task<string> GetRuntimeInfoAsync(CancellationToken cancellationToken = default)
    {
        var host = Clean(await RunAsync("hostname", [], cancellationToken));
        var uptime = Clean(await RunAsync("uptime", ["-p"], cancellationToken)).Replace("up ", "");
        var bootTime = Clean(await RunAsync("who", ["-b"], cancellationToken));
        var dobbyState = Clean(await RunAsync("systemctl", ["is-active", "thisisdobby"], cancellationToken));
        var dobbyEnabled = Clean(await RunAsync("systemctl", ["is-enabled", "thisisdobby"], cancellationToken));
        var activeSince = Clean(await RunAsync(
            "systemctl",
            ["show", "thisisdobby", "--property=ActiveEnterTimestamp", "--value"],
            cancellationToken));

        return $"""
🕒 Runtime / Boot Info

Host:
{host}

System Uptime:
{uptime}

Boot Time:
{bootTime}

Dobby Service:
• Active: {dobbyState}
• Enabled: {dobbyEnabled}
• Active Since: {activeSince}
""";
    }

    public async Task<string> GetDobbyLogsAsync(CancellationToken cancellationToken = default)
    {
        var logs = await RunAsync(
            "journalctl",
            ["-u", "thisisdobby", "-n", "80", "--no-pager"],
            cancellationToken);

        if (string.IsNullOrWhiteSpace(logs))
        {
            return """
📜 Dobby Logs

Log tapılmadı.
""";
        }

        var lines = logs
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .TakeLast(80)
            .Select(x => x.Length <= 160 ? x : x[..160] + "...");

        return $"""
📜 Dobby Logs

{string.Join('\n', lines)}
""";
    }

    public async Task<string> RebootAsync(CancellationToken cancellationToken = default)
    {
        _ = await RunAsync(
            "sudo",
            ["-n", "systemctl", "reboot"],
            cancellationToken);

        return """
🔁 Reboot command sent.

Əgər server reboot etmədisə, systemd service user üçün sudoers icazəsi yoxdur.
""";
    }

    public async Task<string> PowerOffAsync(CancellationToken cancellationToken = default)
    {
        _ = await RunAsync(
            "sudo",
            ["-n", "systemctl", "poweroff"],
            cancellationToken);

        return """
⏻ Power off command sent.

Əgər server sönmədisə, systemd service user üçün sudoers icazəsi yoxdur.
""";
    }

    private static async Task<string> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process();

            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            return string.IsNullOrWhiteSpace(output)
                ? error
                : output;
        }
        catch
        {
            return "";
        }
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : value.Trim();
    }
}