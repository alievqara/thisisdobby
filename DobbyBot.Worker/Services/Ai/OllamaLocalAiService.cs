using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DobbyBot.Worker.Options;
using Microsoft.Extensions.Options;

namespace DobbyBot.Worker.Services.Ai;

public sealed class OllamaLocalAiService : ILocalAiService
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
    private readonly LocalAiOptions _options;
    private readonly ILogger<OllamaLocalAiService> _logger;

    public OllamaLocalAiService(
        HttpClient httpClient,
        IOptions<LocalAiOptions> options,
        ILogger<OllamaLocalAiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> AskAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return "Sual yaz: /ai Docker nedir?";
        }

        var request = new OllamaChatRequest(
            Model: _options.Model,
            Stream: false,
            Think: false,
            KeepAlive: "10m",
            Messages:
            [
                new OllamaChatMessage("system", SystemPrompt),
                new OllamaChatMessage("user", question.Trim())
            ],
            Options: new OllamaChatOptions(
                Temperature: _options.Temperature,
                NumPredict: _options.MaxOutputTokens));

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "/api/chat",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogWarning(
                    "Local AI returned non-success status. StatusCode: {StatusCode}, Body: {Body}",
                    response.StatusCode,
                    body);

                return "Local AI cavab verə bilmədi. Ollama servisində problem ola bilər.";
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                cancellationToken);

            var content = result?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                return "Local AI boş cavab qaytardı.";
            }

            return CleanThinkingText(content);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Local AI timeout.");

            return "Local AI gec cavab verdi. Model server üçün ağır ola bilər.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.RequestTimeout)
        {
            _logger.LogWarning(ex, "Local AI request timeout.");

            return "Local AI gec cavab verdi. Model server üçün ağır ola bilər.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected Local AI error.");

            return "Local AI ilə əlaqə zamanı xəta baş verdi.";
        }
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