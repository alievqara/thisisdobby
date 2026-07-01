namespace DobbyBot.Worker.Modules.DevTasks;

public sealed class TaskReportFormatter : ITaskReportFormatter
{
    public string Format(DevTaskResult result)
    {
        if (result.IsSuccess)
        {
            return string.IsNullOrWhiteSpace(result.Output)
                ? "Boş cavab qaytardı."
                : result.Output.Trim();
        }

        return string.IsNullOrWhiteSpace(result.Error)
            ? "Cavab verə bilmədi."
            : result.Error.Trim();
    }
}