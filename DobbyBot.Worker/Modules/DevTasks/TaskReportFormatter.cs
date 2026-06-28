namespace DobbyBot.Worker.Modules.DevTasks;

public sealed class TaskReportFormatter : ITaskReportFormatter
{
    public string Format(DevTaskResult result)
    {
        var status = result.IsSuccess
            ? "✅ Completed"
            : "❌ Failed";

        var outputBlock = string.IsNullOrWhiteSpace(result.Output)
            ? "No output."
            : result.Output;

        var errorBlock = string.IsNullOrWhiteSpace(result.Error)
            ? "No error."
            : result.Error;

        return $"""
        🤖 Dobby Dev Task

        📌 Status
        └ {status}

        ⏱ Duration
        └ {FormatDuration(result.Duration)}

        🧾 Summary
        └ {result.Summary}

        📤 Output
        {outputBlock}

        ⚠️ Error
        {errorBlock}
        """;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
        {
            return $"{duration.TotalMinutes:0.0} min";
        }

        return $"{duration.TotalSeconds:0.0} sec";
    }
}