namespace DobbyBot.Worker.Modules.DevTasks;

public sealed class DevTaskService : IDevTaskService
{
    private readonly IAgentRunnerService _agentRunnerService;
    private readonly ITaskReportFormatter _reportFormatter;
    private readonly ILogger<DevTaskService> _logger;

    public DevTaskService(
        IAgentRunnerService agentRunnerService,
        ITaskReportFormatter reportFormatter,
        ILogger<DevTaskService> logger)
    {
        _agentRunnerService = agentRunnerService;
        _reportFormatter = reportFormatter;
        _logger = logger;
    }

    public async Task<string> HandleAsync(
        long telegramUserId,
        string message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received dev task from Telegram user {TelegramUserId}. MessageLength: {MessageLength}",
            telegramUserId,
            message.Length);

        var request = new DevTaskRequest(
            telegramUserId,
            message);

        var result = await _agentRunnerService.RunAsync(
            request,
            cancellationToken);

        return _reportFormatter.Format(result);
    }
}