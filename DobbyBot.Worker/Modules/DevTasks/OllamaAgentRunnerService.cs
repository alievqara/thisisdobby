using System.Diagnostics;
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
    - İstifadəçinin yazdığı mesaja birbaşa cavab ver.
    - Əgər istifadəçi sadəcə salam verirsə, qısa salamla cavab ver.
    - Əgər sual aydın deyilsə, uydurma cavab yazma, dəqiqləşdirici sual ver.
    - Server, Docker, Linux, PostgreSQL, ASP.NET Core mövzularında praktik və qısa cavab ver.
    - Kod və ya terminal komandası yalnız istifadəçi istəyəndə ver.
    - Təhlükəli əmrlər üçün xəbərdarlıq et.
    - Lazımsız uzun izah yazma.
    - Düşünmə prosesini cavabda göstərmə.
    - <think> blokları yazma.
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
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            stopwatch.Stop();

            return Fail(
                "Sual boşdur.",
                stopwatch.Elapsed);
        }

        var body = new OllamaChatRequest(
            Model: _options.OllamaModel,
            Stream: false,
            Think: false,
            KeepAlive: "10m",
            Messages:
            [
                new OllamaChatMessage("system", SystemPrompt),
                new OllamaChatMessage("user", request.Message.Trim())
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
                stopwatch.Stop();

                _logger.LogWarning(
                    "Ollama returned non-success status. StatusCode: {StatusCode}, Body: {Body}",
                    response.StatusCode,
                    raw);

                return Fail(
                    "Local AI cavab verə bilmədi. Ollama servisində problem ola bilər.",
                    stopwatch.Elapsed);
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                cancellationToken);

            var content = result?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                stopwatch.Stop();

                return Fail(
                    "Local AI boş cavab qaytardı.",
                    stopwatch.Elapsed);
            }

            var output = TrimToMaxOutput(
                CleanThinkingText(content));

            stopwatch.Stop();

            return new DevTaskResult(
                IsSuccess: true,
                Summary: "Local AI cavab verdi.",
                Output: output,
                Error: string.Empty,
                Duration: stopwatch.Elapsed);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger.LogWarning(ex, "Ollama request timed out.");

            return Fail(
                "Local AI gec cavab verdi. Model server üçün ağır ola bilər.",
                stopwatch.Elapsed);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.RequestTimeout)
        {
            stopwatch.Stop();

            _logger.LogWarning(ex, "Ollama request timeout.");

            return Fail(
                "Local AI gec cavab verdi. Model server üçün ağır ola bilər.",
                stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Unexpected Ollama error.");

            return Fail(
                "Local AI ilə əlaqə zamanı xəta baş verdi.",
                stopwatch.Elapsed);
        }
    }

    private DevTaskResult Fail(
        string error,
        TimeSpan duration)
    {
        return new DevTaskResult(
            IsSuccess: false,
            Summary: "Local AI xətası.",
            Output: string.Empty,
            Error: error,
            Duration: duration);
    }

    private string TrimToMaxOutput(string value)
    {
        var cleaned = string.IsNullOrWhiteSpace(value)
            ? "Local AI boş cavab qaytardı."
            : value.Trim();

        if (_options.MaxOutputChars <= 0)
        {
            return cleaned;
        }

        return cleaned.Length <= _options.MaxOutputChars
            ? cleaned
            : cleaned[.._options.MaxOutputChars].Trim() + "\n\n...cavab qısaldıldı.";
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