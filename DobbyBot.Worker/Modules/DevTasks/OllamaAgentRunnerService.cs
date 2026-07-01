using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DobbyBot.Worker.Options;
using Microsoft.Extensions.Options;

namespace DobbyBot.Worker.Modules.DevTasks;

public sealed class OllamaAgentRunnerService : IAgentRunnerService
{
    private const string SystemPrompt = """
Sən ThisIsDobby HomeLab botunun lokal AI köməkçisisən.

Qaydalar:
- Qısa və praktik cavab ver.
- Server, Docker, Linux, PostgreSQL, ASP.NET Core mövzularında konkret ol.
- Lazımsız uzun izah yazma.
- Komanda verəndə təhlükəli əmrləri xəbərdarlıqla göstər.
- Əmin deyilsənsə, bunu açıq de.
- Düşünmə prosesini cavabda göstərmə.
""";

    private readonly HttpClient _httpClient;
    private readonly AiTaskOptions _options;
    private readonly ILogger<OllamaAgentRunnerService> _logger;

    public OllamaAgentRunnerService(
        HttpClient httpClient,
        IOptions<AiTaskOptions> options,
        ILogger<OllamaAgentRunnerService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DevTaskResult> RunAsync(
        DevTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return DevTaskResult.Failed("Sual boşdur.");
        }

        var prompt = BuildUserPrompt(request);

        var body = new OllamaChatRequest(
            Model: _options.OllamaModel,
            Stream: false,
            Think: false,
            KeepAlive: "10m",
            Messages:
            [
                new OllamaChatMessage("system", SystemPrompt),
                new OllamaChatMessage("user", prompt)
            ],
            Options: new OllamaChatOptions(
                Temperature: _options.OllamaTemperature,
                NumPredict: _options.OllamaMaxOutputTokens));

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "/api/chat",
                body,
                cancellationToken);

            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Ollama returned non-success status. StatusCode: {StatusCode}, Body: {Body}",
                    response.StatusCode,
                    raw);

                return DevTaskResult.Failed(
                    "Local AI cavab verə bilmədi. Ollama servisində problem ola bilər.");
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                cancellationToken);

            var content = result?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                return DevTaskResult.Failed("Local AI boş cavab qaytardı.");
            }

            var cleaned = CleanThinkingText(content);

            return DevTaskResult.Success(TrimToMaxOutput(cleaned));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Ollama request timed out.");

            return DevTaskResult.Failed(
                "Local AI gec cavab verdi. Model server üçün ağır ola bilər.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.RequestTimeout)
        {
            _logger.LogWarning(ex, "Ollama request timeout.");

            return DevTaskResult.Failed(
                "Local AI gec cavab verdi. Model server üçün ağır ola bilər.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected Ollama error.");

            return DevTaskResult.Failed(
                "Local AI ilə əlaqə zamanı xəta baş verdi.");
        }
    }

    private static string BuildUserPrompt(DevTaskRequest request)
    {
        return $"""
Telegram user id:
{request.TelegramUserId}

User message:
{request.Text}
""";
    }

    private string TrimToMaxOutput(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Local AI boş cavab qaytardı.";
        }

        if (_options.MaxOutputChars <= 0)
        {
            return value.Trim();
        }

        return value.Length <= _options.MaxOutputChars
            ? value.Trim()
            : value[.._options.MaxOutputChars].Trim() + "\n\n...cavab qısaldıldı.";
    }

    private static string CleanThinkingText(string value)
    {
        var cleaned = value.Trim();

        cleaned = Regex.Replace(
            cleaned,
            "<think>.*?</think>",
            "",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        cleaned = Regex.Replace(
            cleaned,
            @"Thinking\.\.\..*?done thinking\.",
            "",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        cleaned = cleaned
            .Replace("<think>", "", StringComparison.OrdinalIgnoreCase)
            .Replace("</think>", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        return string.IsNullOrWhiteSpace(cleaned)
            ? "Local AI cavab verdi, amma cavab təmizləndikdən sonra boş qaldı."
            : cleaned;
    }

    private sealed record OllamaChatRequest(
        string Model,
        bool Stream,
        bool Think,
        string KeepAlive,
        IReadOnlyList<OllamaChatMessage> Messages,
        OllamaChatOptions Options);

    private sealed record OllamaChatMessage(
        string Role,
        string Content);

    private sealed record OllamaChatOptions(
        double Temperature,
        int NumPredict);

    private sealed record OllamaChatResponse(
        OllamaChatMessage? Message);
}