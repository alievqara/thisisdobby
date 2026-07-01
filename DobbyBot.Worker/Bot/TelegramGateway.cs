using DobbyBot.Worker.Options;
using DobbyBot.Worker.TextRouting;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
namespace DobbyBot.Worker.Bot;

public sealed class TelegramGateway
{
    private readonly HttpClient _httpClient;
    private readonly DobbyBotOptions _options;
    private readonly ILogger<TelegramGateway> _logger;
    private readonly ITextMessageRouter _textMessageRouter;

    public TelegramGateway(
        HttpClient httpClient,
        IOptions<DobbyBotOptions> options,
        ITextMessageRouter textMessageRouter,
        ILogger<TelegramGateway> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _textMessageRouter = textMessageRouter;
    }

    public async Task<IReadOnlyCollection<TelegramUpdate>> GetUpdatesAsync(
    long offset,
    CancellationToken cancellationToken)
    {
        var url =
            $"{BuildTelegramApiUrl("getUpdates")}?timeout=25&offset={offset}&allowed_updates=[\"message\",\"callback_query\"]";

        using var response = await _httpClient.GetAsync(
            url,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new HttpRequestException(
                "Telegram bot token is unauthorized.",
                null,
                HttpStatusCode.Unauthorized);
        }

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogWarning(
                "Failed to get Telegram updates. StatusCode: {StatusCode}, Response: {Response}",
                response.StatusCode,
                responseBody);

            return [];
        }

        var result = await response.Content.ReadFromJsonAsync<TelegramApiResponse<List<TelegramUpdate>>>(
            cancellationToken);

        return result?.Result ?? [];
    }
    

    public async Task<TelegramMessage?> SendMessageAsync(
     long chatId,
     string text,
     TelegramInlineKeyboardMarkup? replyMarkup,
     CancellationToken cancellationToken)
    {
        var url = $"https://api.telegram.org/bot{_options.Token}/sendMessage";

        var payload = new
        {
            chat_id = chatId,
            text,
            reply_markup = replyMarkup
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                url,
                payload,
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Telegram sendMessage failed. StatusCode: {StatusCode}, Body: {Body}",
                    response.StatusCode,
                    body);

                return null;
            }

            var result = JsonSerializer.Deserialize<TelegramApiResponse<TelegramMessage>>(body);

            return result?.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram sendMessage failed.");
            return null;
        }
    }

    private string BuildTelegramApiUrl(string methodName)
    {
        return $"https://api.telegram.org/bot{_options.Token}/{methodName}";
    }

    public async Task<bool> SetMyCommandsAsync(
        IReadOnlyCollection<TelegramBotCommand> commands,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            BuildTelegramApiUrl("setMyCommands"),
            new
            {
                commands
            },
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogWarning(
            "Failed to set Telegram bot commands. StatusCode: {StatusCode}, Response: {Response}",
            response.StatusCode,
            responseBody);

        return false;
    }

    public async Task<bool> EditMessageTextAsync(
        long chatId,
        long messageId,
        string text,
        TelegramInlineKeyboardMarkup? replyMarkup,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.telegram.org/bot{_options.Token}/editMessageText";

        var payload = new
        {
            chat_id = chatId,
            message_id = messageId,
            text,
            reply_markup = replyMarkup
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                url,
                payload,
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            if (body.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            _logger.LogWarning(
                "Telegram editMessageText failed. ChatId: {ChatId}, MessageId: {MessageId}, StatusCode: {StatusCode}, Body: {Body}",
                chatId,
                messageId,
                response.StatusCode,
                body);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Telegram editMessageText failed. ChatId: {ChatId}, MessageId: {MessageId}",
                chatId,
                messageId);

            return false;
        }
    }

    public async Task<bool> AnswerCallbackQueryAsync(
        string callbackQueryId,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.telegram.org/bot{_options.Token}/answerCallbackQuery";

        var payload = new
        {
            callback_query_id = callbackQueryId
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                url,
                payload,
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _logger.LogWarning(
                "Telegram answerCallbackQuery failed. StatusCode: {StatusCode}, Body: {Body}",
                response.StatusCode,
                body);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram answerCallbackQuery failed.");
            return false;
        }
    }

    public async Task<bool> DeleteMessageAsync(
        long chatId,
        long messageId,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.telegram.org/bot{_options.Token}/deleteMessage";

        var payload = new
        {
            chat_id = chatId,
            message_id = messageId
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                url,
                payload,
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _logger.LogDebug(
                "Telegram deleteMessage failed. ChatId: {ChatId}, MessageId: {MessageId}, StatusCode: {StatusCode}, Body: {Body}",
                chatId,
                messageId,
                response.StatusCode,
                body);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Telegram deleteMessage failed. ChatId: {ChatId}, MessageId: {MessageId}",
                chatId,
                messageId);

            return false;
        }
    }
}