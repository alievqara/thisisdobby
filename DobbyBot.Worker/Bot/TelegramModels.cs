using System.Text.Json.Serialization;

namespace DobbyBot.Worker.Bot;

public sealed class TelegramApiResponse<T>
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("result")]
    public T? Result { get; init; }
}

public sealed class TelegramUpdate
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; init; }

    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; init; }

    [JsonPropertyName("callback_query")]
    public TelegramCallbackQuery? CallbackQuery { get; init; }
}

public sealed class TelegramCallbackQuery
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("from")]
    public TelegramUser From { get; init; } = new();

    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; init; }

    [JsonPropertyName("data")]
    public string? Data { get; init; }
}

public sealed class TelegramMessage
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; init; }

    [JsonPropertyName("from")]
    public TelegramUser? From { get; init; }

    [JsonPropertyName("chat")]
    public TelegramChat Chat { get; init; } = new();

    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

public sealed class TelegramUser
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }
}

public sealed class TelegramChat
{
    [JsonPropertyName("id")]
    public long Id { get; init; }
}

public sealed class TelegramInlineKeyboardMarkup
{
    [JsonPropertyName("inline_keyboard")]
    public required TelegramInlineKeyboardButton[][] InlineKeyboard { get; init; }
}

public sealed class TelegramInlineKeyboardButton
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("callback_data")]
    public required string CallbackData { get; init; }
}