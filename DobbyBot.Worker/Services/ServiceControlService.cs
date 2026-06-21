using System.Text;
using DobbyBot.Worker.Execution;

namespace DobbyBot.Worker.Services;

public sealed class ServiceControlService : IServiceControlService
{
    private readonly ICommandExecutor _executor;

    private static readonly IReadOnlyDictionary<string, string> AllowedServices =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["golotto"] = "golotto.service"
        };

    public ServiceControlService(ICommandExecutor executor)
    {
        _executor = executor;
    }

    public async Task<string> GetServicesAsync(CancellationToken cancellationToken)
    {
        var result = new StringBuilder();

        result.AppendLine("🧦 Dobby Services");
        result.AppendLine();

        foreach (var service in AllowedServices)
        {
            var status = await _executor.RunAsync(
                "/usr/bin/systemctl",
                ["is-active", service.Value],
                cancellationToken);

            var statusText = status.IsSuccess
                ? status.Output
                : "unknown";

            result.AppendLine($"{service.Key}: {statusText}");
        }

        return result.ToString();
    }

    public async Task<string> RestartAsync(
        string serviceKey,
        CancellationToken cancellationToken)
    {
        if (!AllowedServices.TryGetValue(serviceKey, out var serviceName))
        {
            return $"Service icazəli deyil: {serviceKey}";
        }

        var result = await _executor.RunAsync(
            "/usr/bin/sudo",
            ["/usr/bin/systemctl", "restart", serviceName],
            cancellationToken);

        if (!result.IsSuccess)
        {
            return $"❌ Restart alınmadı: {serviceKey}\n{result.Error}";
        }

        return $"✅ Restart edildi: {serviceKey}";
    }

    public async Task<string> LogsAsync(
        string serviceKey,
        CancellationToken cancellationToken)
    {
        if (!AllowedServices.TryGetValue(serviceKey, out var serviceName))
        {
            return $"Service icazəli deyil: {serviceKey}";
        }

        var result = await _executor.RunAsync(
            "/usr/bin/sudo",
            ["/usr/bin/journalctl", "-u", serviceName, "-n", "80", "--no-pager"],
            cancellationToken);

        if (!result.IsSuccess)
        {
            return $"❌ Log oxunmadı: {serviceKey}\n{result.Error}";
        }

        return LimitTelegramMessage(result.Output);
    }

    private static string LimitTelegramMessage(string text)
    {
        const int maxLength = 3500;

        if (string.IsNullOrWhiteSpace(text))
        {
            return "Log boşdur.";
        }

        return text.Length <= maxLength
            ? text
            : text[^maxLength..];
    }
}