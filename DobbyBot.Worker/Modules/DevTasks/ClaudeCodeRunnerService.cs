using System.Diagnostics;
using DobbyBot.Worker.Options;
using Microsoft.Extensions.Options;

namespace DobbyBot.Worker.Modules.DevTasks;

public sealed class ClaudeCodeRunnerService : IAgentRunnerService
{
    private readonly AiTaskOptions _options;
    private readonly ILogger<ClaudeCodeRunnerService> _logger;

    public ClaudeCodeRunnerService(
        IOptions<AiTaskOptions> options,
        ILogger<ClaudeCodeRunnerService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DevTaskResult> RunAsync(
        DevTaskRequest request,
        CancellationToken cancellationToken)
    {
        ValidateOptions();

        var startedAt = DateTime.UtcNow;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);

        timeoutCts.CancelAfter(
            TimeSpan.FromMinutes(_options.TaskTimeoutMinutes));

        try
        {
            var prompt = BuildClaudePrompt(request.Message);

            using var process = new Process();

            process.StartInfo = new ProcessStartInfo
            {
                FileName = _options.ClaudeCodeCommand,
                WorkingDirectory = _options.PlanzyRepoPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();

            await process.StandardInput.WriteLineAsync(
                prompt.AsMemory(),
                timeoutCts.Token);

            process.StandardInput.Close();

            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await process.WaitForExitAsync(timeoutCts.Token);

            var output = await outputTask;
            var error = await errorTask;

            var duration = DateTime.UtcNow - startedAt;

            return new DevTaskResult(
                IsSuccess: process.ExitCode == 0,
                Summary: process.ExitCode == 0
                    ? "Claude Code task tamamlandı."
                    : $"Claude Code exit code: {process.ExitCode}",
                Output: Limit(output),
                Error: Limit(error),
                Duration: duration);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var duration = DateTime.UtcNow - startedAt;

            return new DevTaskResult(
                IsSuccess: false,
                Summary: "AI task timeout oldu.",
                Output: string.Empty,
                Error: $"Task {_options.TaskTimeoutMinutes} dəqiqə içində bitmədi.",
                Duration: duration);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Claude Code runner failed.");

            var duration = DateTime.UtcNow - startedAt;

            return new DevTaskResult(
                IsSuccess: false,
                Summary: "Claude Code runner xətaya düşdü.",
                Output: string.Empty,
                Error: ex.Message,
                Duration: duration);
        }
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.PlanzyRepoPath))
        {
            throw new InvalidOperationException("AiTask:PlanzyRepoPath is missing.");
        }

        if (!Directory.Exists(_options.PlanzyRepoPath))
        {
            throw new InvalidOperationException(
                $"Planzy repo path does not exist: {_options.PlanzyRepoPath}");
        }

        if (string.IsNullOrWhiteSpace(_options.ClaudeCodeCommand))
        {
            throw new InvalidOperationException("AiTask:ClaudeCodeCommand is missing.");
        }
    }

    private static string BuildClaudePrompt(string userMessage)
    {
        return $"""
        You are working inside the Planzy repository.

        Act as a senior .NET/React full-stack developer.
        Follow SOLID, clean architecture, security and production-quality code.
        Do not make destructive changes unless explicitly requested.
        Keep changes focused on the user's task.
        After finishing, provide a concise summary of what you changed or checked.
        If build/test commands are needed, run them and summarize the result.

        User task:
        {userMessage}
        """;
    }

    private string Limit(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();

        if (trimmed.Length <= _options.MaxOutputChars)
        {
            return trimmed;
        }

        return trimmed[^_options.MaxOutputChars..];
    }
}