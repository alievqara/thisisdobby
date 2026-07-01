using System.Net;
using DobbyBot.Worker.Commands;
using DobbyBot.Worker.Options;
using DobbyBot.Worker.Security;
using Microsoft.Extensions.Options;

namespace DobbyBot.Worker.Bot;

public sealed class DobbyBotWorker : BackgroundService
{
    private readonly ILogger<DobbyBotWorker> _logger;
    private readonly TelegramGateway _telegramGateway;
    private readonly CommandRouter _commandRouter;
    private readonly IAdminGuard _adminGuard;
    private readonly DobbyBotOptions _options;

    public DobbyBotWorker(
        ILogger<DobbyBotWorker> logger,
        TelegramGateway telegramGateway,
        CommandRouter commandRouter,
        IAdminGuard adminGuard,
        IOptions<DobbyBotOptions> options)
    {
        _logger = logger;
        _telegramGateway = telegramGateway;
        _commandRouter = commandRouter;
        _adminGuard = adminGuard;
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
                    Description = "Open Dobby menu"
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

        var text = message.Text.Trim();

        if (text.Equals("/menu", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            var response = await _commandRouter.HandleAsync(
                telegramUserId.Value,
                "/menu",
                cancellationToken);

            await _telegramGateway.SendMessageAsync(
                message.Chat.Id,
                response.Text,
                response.ReplyMarkup,
                cancellationToken);

            await TryDeleteMessageAsync(
                message.Chat.Id,
                message.MessageId,
                cancellationToken);

            return;
        }

        await _telegramGateway.SendMessageAsync(
            message.Chat.Id,
            "🧦 Dobby yalnız menu ilə idarə olunur. /menu yaz.",
            null,
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

        await _telegramGateway.AnswerCallbackQueryAsync(
            callbackQuery.Id,
            cancellationToken);

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
}