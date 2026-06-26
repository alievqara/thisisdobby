using DobbyBot.Worker.Options;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace DobbyBot.Worker.Bot;

public sealed class TelegramGateway
{
    private readonly HttpClient _httpClient;
    private readonly DobbyBotOptions _options;
    private readonly ILogger<TelegramGateway> _logger;

    public TelegramGateway(
        HttpClient httpClient,
        IOptions<DobbyBotOptions> options,
        ILogger<TelegramGateway> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(
        long offset,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://api.telegram.org/bot{_options.Token}/getUpdates" +
            $"?timeout=25&offset={offset}" +
            $"&allowed_updates=%5B%22message%22,%22callback_query%22%5D";

        try
        {
            var response =
                await _httpClient.GetFromJsonAsync<TelegramApiResponse<List<TelegramUpdate>>>(
                    url,
                    cancellationToken);

            return response?.Result ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Telegram updates.");
            return [];
        }
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