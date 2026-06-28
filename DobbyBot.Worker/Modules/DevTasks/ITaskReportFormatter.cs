namespace DobbyBot.Worker.Modules.DevTasks;

public interface ITaskReportFormatter
{
    string Format(DevTaskResult result);
}