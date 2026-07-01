using System.Net;
using DobbyBot.Worker.Commands;
using DobbyBot.Worker.Options;
using DobbyBot.Worker.Security;
using DobbyBot.Worker.Services.Ai;
using DobbyBot.Worker.State;
using DobbyBot.Worker.TextRouting;
using Microsoft.Extensions.Options;

namespace DobbyBot.Worker.Bot;

public sealed class DobbyBotWorker : BackgroundService
{
    private readonly ILogger<DobbyBotWorker> _logger;
    private readonly TelegramGateway _telegramGateway;
    private readonly CommandRouter _commandRouter;
    private readonly IAdminGuard _adminGuard;
    private readonly IUserStateService _userStateService;
    private readonly ITextMessageRouter _textMessageRouter;
    private readonly ILocalAiService _localAiService;
    private readonly DobbyBotOptions _options;

    public DobbyBotWorker(
        ILogger<DobbyBotWorker> logger,
        TelegramGateway telegramGateway,
        CommandRouter commandRouter,
        IAdminGuard adminGuard,
        IUserStateService userStateService,
        ITextMessageRouter textMessageRouter,
        ILocalAiService localAiService,
        IOptions<DobbyBotOptions> options)
    {
        _logger = logger;
        _telegramGateway = telegramGateway;
        _commandRouter = commandRouter;
        _adminGuard = adminGuard;
        _userStateService = userStateService;
        _textMessageRouter = textMessageRouter;
        _localAiService = localAiService;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateOptions();

        await RegisterBotCommandsAsync(stoppingToken);

        var offset = 0L;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await _telegramGateway.GetUpdatesAsync(
                    offset,
                    stoppingToken);

                foreach (var update in updates)
                {
                    offset = Math.Max(offset, update.UpdateId + 1);

                    await HandleUpdateAsync(
                        update,
                        stoppingToken);
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogCritical(
                    ex,
                    "Telegram bot token is unauthorized. Check DobbyBot__Token. Polling stopped.");

                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dobby polling loop failed.");

                await Task.Delay(
                    TimeSpan.FromSeconds(3),
                    stoppingToken);
            }
        }
    }

    private async Task RegisterBotCommandsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var commands = new[]
            {
                new TelegramBotCommand
                {
                    Command = "menu",
                    Description = "Open menu"
                },
                new TelegramBotCommand
                {
                    Command = "status",
                    Description = "HomeLab status"
                },
                new TelegramBotCommand
                {
                    Command = "docker",
                    Description = "Docker groups"
                },
                new TelegramBotCommand
                {
                    Command = "ai",
                    Description = "Ask local AI"
                },
                new TelegramBotCommand
                {
                    Command = "ask",
                    Description = "Ask local AI"
                },
                new TelegramBotCommand
                {
                    Command = "help",
                    Description = "Help"
                }
            };

            var registered = await _telegramGateway.SetMyCommandsAsync(
                commands,
                cancellationToken);

            if (!registered)
            {
                _logger.LogWarning("Telegram bot commands were not registered.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to register Telegram bot commands. Bot will continue without command menu.");
        }
    }

    private async Task HandleUpdateAsync(
        TelegramUpdate update,
        CancellationToken cancellationToken)
    {
        if (update.Message is not null)
        {
            await HandleMessageAsync(
                update.Message,
                cancellationToken);

            return;
        }

        if (update.CallbackQuery is not null)
        {
            await HandleCallbackQueryAsync(
                update.CallbackQuery,
                cancellationToken);
        }
    }

    private async Task HandleMessageAsync(
        TelegramMessage message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        var telegramUserId = message.From?.Id;

        if (telegramUserId is null)
        {
            return;
        }

        if (!_adminGuard.IsAdmin(telegramUserId.Value))
        {
            _logger.LogWarning(
                "Unauthorized Telegram user ignored. UserId: {UserId}",
                telegramUserId.Value);

            return;
        }

        if (IsLocalAiCommand(message.Text))
        {
            await HandleLocalAiMessageAsync(
                message.Chat.Id,
                message.MessageId,
                message.Text,
                cancellationToken);

            return;
        }

        if (_textMessageRouter.IsCommand(message.Text))
        {
            await HandleCommandMessageAsync(
                telegramUserId.Value,
                message.Chat.Id,
                message.MessageId,
                message.Text,
                cancellationToken);

            return;
        }

        await HandlePlainTextMessageAsync(
            telegramUserId.Value,
            message.Chat.Id,
            message.Text,
            cancellationToken);
    }

    private async Task HandleCommandMessageAsync(
        long telegramUserId,
        long chatId,
        long messageId,
        string text,
        CancellationToken cancellationToken)
    {
        var response = await _commandRouter.HandleAsync(
            telegramUserId,
            text,
            cancellationToken);

        await _telegramGateway.SendMessageAsync(
            chatId,
            response.Text,
            response.ReplyMarkup,
            cancellationToken);

        await TryDeleteMessageAsync(
            chatId,
            messageId,
            cancellationToken);
    }

    private async Task HandlePlainTextMessageAsync(
        long telegramUserId,
        long chatId,
        string text,
        CancellationToken cancellationToken)
    {
        await _telegramGateway.SendMessageAsync(
            chatId,
            "🤖 Task qəbul edildi. Claude Code işləyir...",
            null,
            cancellationToken);

        var responseText = await _textMessageRouter.HandlePlainTextAsync(
            telegramUserId,
            text,
            cancellationToken);

        await _telegramGateway.SendMessageAsync(
            chatId,
            responseText,
            null,
            cancellationToken);
    }

    private async Task HandleLocalAiMessageAsync(
        long chatId,
        long messageId,
        string text,
        CancellationToken cancellationToken)
    {
        var question = ExtractLocalAiQuestion(text);

        if (string.IsNullOrWhiteSpace(question))
        {
            await _telegramGateway.SendMessageAsync(
                chatId,
                "Sual yaz: /ai Docker nedir?",
                null,
                cancellationToken);

            await TryDeleteMessageAsync(
                chatId,
                messageId,
                cancellationToken);

            return;
        }

        await _telegramGateway.SendMessageAsync(
            chatId,
            "🤖 Local AI düşünür...",
            null,
            cancellationToken);

        var answer = await _localAiService.AskAsync(
            question,
            cancellationToken);

        foreach (var chunk in SplitTelegramMessage(answer))
        {
            await _telegramGateway.SendMessageAsync(
                chatId,
                chunk,
                null,
                cancellationToken);
        }

        await TryDeleteMessageAsync(
            chatId,
            messageId,
            cancellationToken);
    }

    private async Task HandleCallbackQueryAsync(
        TelegramCallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(callbackQuery.Data))
        {
            return;
        }

        var telegramUserId = callbackQuery.From.Id;

        if (!_adminGuard.IsAdmin(telegramUserId))
        {
            _logger.LogWarning(
                "Unauthorized callback ignored. UserId: {UserId}",
                telegramUserId);

            return;
        }

        var response = await _commandRouter.HandleCallbackAsync(
            telegramUserId,
            callbackQuery.Data,
            cancellationToken);

        if (response.SendAsNewMessage)
        {
            await _telegramGateway.SendMessageAsync(
                callbackQuery.Message.Chat.Id,
                response.Text,
                response.ReplyMarkup,
                cancellationToken);

            return;
        }

        var edited = await _telegramGateway.EditMessageTextAsync(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            response.Text,
            response.ReplyMarkup,
            cancellationToken);

        if (!edited)
        {
            await _telegramGateway.SendMessageAsync(
                callbackQuery.Message.Chat.Id,
                response.Text,
                response.ReplyMarkup,
                cancellationToken);
        }
    }

    private async Task TryDeleteMessageAsync(
        long chatId,
        long messageId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _telegramGateway.DeleteMessageAsync(
                chatId,
                messageId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Failed to delete Telegram message. ChatId: {ChatId}, MessageId: {MessageId}",
                chatId,
                messageId);
        }
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            throw new InvalidOperationException("DobbyBot__Token is missing.");
        }

        if (_options.AdminTelegramId <= 0)
        {
            throw new InvalidOperationException("DobbyBot__AdminTelegramId is missing or invalid.");
        }
    }

    private static bool IsLocalAiCommand(string text)
    {
        return text.StartsWith("/ai", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("/ask", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractLocalAiQuestion(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.StartsWith("/ai", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Length <= 3
                ? ""
                : trimmed[3..].Trim();
        }

        if (trimmed.StartsWith("/ask", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Length <= 4
                ? ""
                : trimmed[4..].Trim();
        }

        return "";
    }

    private static IReadOnlyList<string> SplitTelegramMessage(
        string text,
        int maxLength = 3900)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ["Local AI boş cavab qaytardı."];
        }

        if (text.Length <= maxLength)
        {
            return [text];
        }

        var chunks = new List<string>();
        var remaining = text;

        while (remaining.Length > maxLength)
        {
            var splitAt = remaining.LastIndexOf('\n', maxLength);

            if (splitAt < maxLength / 2)
            {
                splitAt = maxLength;
            }

            chunks.Add(remaining[..splitAt].Trim());
            remaining = remaining[splitAt..].Trim();
        }

        if (!string.IsNullOrWhiteSpace(remaining))
        {
            chunks.Add(remaining);
        }

        return chunks;
    }
}