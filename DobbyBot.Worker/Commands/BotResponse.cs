using DobbyBot.Worker.Bot;

namespace DobbyBot.Worker.Commands;

public sealed record BotResponse(
    string Text,
    TelegramInlineKeyboardMarkup? ReplyMarkup = null,
    bool SendAsNewMessage = false);