using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DobbyBot.Worker.Options;
using Microsoft.Extensions.Options;

namespace DobbyBot.Worker.Bot;

public sealed class TelegramGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
                RedactTelegramToken(responseBody));

            return [];
        }

        var result = await response.Content.ReadFromJsonAsync<TelegramApiResponse<List<TelegramUpdate>>>(
            JsonOptions,
            cancellationToken);

        return result?.Result ?? [];
    }

    public async Task<TelegramMessage?> SendMessageAsync(
        long chatId,
        string text,
        TelegramInlineKeyboardMarkup? replyMarkup,
        CancellationToken cancellationToken)
    {
        var url = BuildTelegramApiUrl("sendMessage");

        object payload = replyMarkup is null
            ? new
            {
                chat_id = chatId,
                text,
                disable_web_page_preview = true
            }
            : new
            {
                chat_id = chatId,
                text,
                reply_markup = replyMarkup,
                disable_web_page_preview = true
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
                    RedactTelegramToken(body));

                return null;
            }

            var result = JsonSerializer.Deserialize<TelegramApiResponse<TelegramMessage>>(
                body,
                JsonOptions);

            return result?.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram sendMessage failed.");
            return null;
        }
    }

    public async Task<bool> SetMyCommandsAsync(
        IReadOnlyCollection<TelegramBotCommand> commands,
        CancellationToken cancellationToken)
    {
        var url = BuildTelegramApiUrl("setMyCommands");

        var payload = new
        {
            commands
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                url,
                payload,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogWarning(
                "Failed to set Telegram bot commands. StatusCode: {StatusCode}, Response: {Response}",
                response.StatusCode,
                RedactTelegramToken(responseBody));

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set Telegram bot commands.");
            return false;
        }
    }

    public async Task<bool> EditMessageTextAsync(
        long chatId,
        long messageId,
        string text,
        TelegramInlineKeyboardMarkup? replyMarkup,
        CancellationToken cancellationToken)
    {
        var url = BuildTelegramApiUrl("editMessageText");

        object payload = replyMarkup is null
            ? new
            {
                chat_id = chatId,
                message_id = messageId,
                text
            }
            : new
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
                RedactTelegramToken(body));

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
        var url = BuildTelegramApiUrl("answerCallbackQuery");

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
                RedactTelegramToken(body));

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
        var url = BuildTelegramApiUrl("deleteMessage");

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
                RedactTelegramToken(body));

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

    private string BuildTelegramApiUrl(string methodName)
    {
        return $"https://api.telegram.org/bot{_options.Token.Trim()}/{methodName}";
    }

    private string RedactTelegramToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Replace(_options.Token, "***BOT_TOKEN***");
    }
}