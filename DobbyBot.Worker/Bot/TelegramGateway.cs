using System.Net.Http.Json;
using DobbyBot.Worker.Options;
using Microsoft.Extensions.Options;

namespace DobbyBot.Worker.Bot;

public sealed class TelegramGateway
{
    private readonly HttpClient _httpClient;
    private readonly DobbyBotOptions _options;

    public TelegramGateway(
        HttpClient httpClient,
        IOptions<DobbyBotOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(
        long offset,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://api.telegram.org/bot{_options.Token}/getUpdates" +
            $"?timeout=25&offset={offset}" +
            $"&allowed_updates=%5B%22message%22,%22callback_query%22%5D";

        var response =
            await _httpClient.GetFromJsonAsync<TelegramApiResponse<List<TelegramUpdate>>>(
                url,
                cancellationToken);

        return response?.Result ?? [];
    }

    public async Task SendMessageAsync(
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

        using var response = await _httpClient.PostAsJsonAsync(
            url,
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task EditMessageTextAsync(
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

        using var response = await _httpClient.PostAsJsonAsync(
            url,
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task AnswerCallbackQueryAsync(
        string callbackQueryId,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.telegram.org/bot{_options.Token}/answerCallbackQuery";

        var payload = new
        {
            callback_query_id = callbackQueryId
        };

        using var response = await _httpClient.PostAsJsonAsync(
            url,
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}