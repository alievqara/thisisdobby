using System.Net;
using DobbyBot.Worker.Commands;
using DobbyBot.Worker.Security;

namespace DobbyBot.Worker.Bot;

public sealed class DobbyBotWorker : BackgroundService
{
    private const string MenuOnlyMessage =
        "🧦 Dobby yalnız menu ilə idarə olunur. /menu yaz.";

    private readonly ILogger<DobbyBotWorker> _logger;
    private readonly TelegramGateway _telegramGateway;
    private readonly CommandRouter _commandRouter;
    private readonly IAdminGuard _adminGuard;

    public DobbyBotWorker(
        ILogger<DobbyBotWorker> logger,
        TelegramGateway telegramGateway,
        CommandRouter commandRouter,
        IAdminGuard adminGuard)
    {
        _logger = logger;
        _telegramGateway = telegramGateway;
        _commandRouter = commandRouter;
        _adminGuard = adminGuard;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
            catch (HttpRequestException ex)
                when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogCritical(
                    "Telegram bot token is unauthorized. Check DobbyBot__Token. Polling stopped.");

                return;
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Dobby polling loop failed.");

                await Task.Delay(
                    TimeSpan.FromSeconds(3),
                    stoppingToken);
            }
        }
    }

    private async Task RegisterBotCommandsAsync(
        CancellationToken cancellationToken)
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
                _logger.LogWarning(
                    "Telegram bot commands were not registered.");
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Failed to register Telegram bot commands. ErrorType: {ErrorType}, Error: {Error}",
                ex.GetType().Name,
                ex.Message);
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

        if (IsMenuCommand(text))
        {
            var response = await _commandRouter.HandleAsync(
                telegramUserId.Value,
                "/menu",
                cancellationToken);

            await SendResponseAsync(
                message.Chat.Id,
                response,
                cancellationToken);

            await TryDeleteMessageAsync(
                message.Chat.Id,
                message.MessageId,
                cancellationToken);

            return;
        }

        if (_commandRouter.ExpectsTextInput(telegramUserId.Value))
        {
            var response = await _commandRouter.HandleAsync(
                telegramUserId.Value,
                text,
                cancellationToken);

            await SendResponseAsync(
                message.Chat.Id,
                response,
                cancellationToken);

            return;
        }

        await _telegramGateway.SendMessageAsync(
            message.Chat.Id,
            MenuOnlyMessage,
            replyMarkup: null,
            cancellationToken);
    }

    private async Task HandleCallbackQueryAsync(
        TelegramCallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        await _telegramGateway.AnswerCallbackQueryAsync(
            callbackQuery.Id,
            cancellationToken);

        if (callbackQuery.Message is null ||
            string.IsNullOrWhiteSpace(callbackQuery.Data))
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

        if (callbackQuery.Data.StartsWith("managed:", StringComparison.Ordinal) &&
            callbackQuery.Data.Contains(":confirm:", StringComparison.Ordinal))
        {
            await _telegramGateway.EditMessageTextAsync(
                callbackQuery.Message.Chat.Id,
                callbackQuery.Message.MessageId,
                "⏳ Əməliyyat yoxlanılır və icra olunur…\n\nGitHub yeniləməsi bir neçə dəqiqə çəkə bilər. Nəticə bu mesajda görünəcək.",
                replyMarkup: null,
                cancellationToken);
        }

        var response = await _commandRouter.HandleCallbackAsync(
            telegramUserId,
            callbackQuery.Data,
            cancellationToken);

        if (response.SendAsNewMessage)
        {
            await SendResponseAsync(
                callbackQuery.Message.Chat.Id,
                response,
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
            await SendResponseAsync(
                callbackQuery.Message.Chat.Id,
                response,
                cancellationToken);
        }
    }

    private Task<TelegramMessage?> SendResponseAsync(
        long chatId,
        BotResponse response,
        CancellationToken cancellationToken)
    {
        return _telegramGateway.SendMessageAsync(
            chatId,
            response.Text,
            response.ReplyMarkup,
            cancellationToken);
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
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                "Failed to delete Telegram message. ChatId: {ChatId}, MessageId: {MessageId}, ErrorType: {ErrorType}, Error: {Error}",
                chatId,
                messageId,
                ex.GetType().Name,
                ex.Message);
        }
    }

    private static bool IsMenuCommand(string text)
    {
        var commandToken = text
            .Split(
                ' ',
                2,
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(commandToken))
        {
            return false;
        }

        var commandWithoutBotName = commandToken
            .Split(
                '@',
                2,
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return commandWithoutBotName is not null &&
               (commandWithoutBotName.Equals(
                    "/menu",
                    StringComparison.OrdinalIgnoreCase) ||
                commandWithoutBotName.Equals(
                    "/start",
                    StringComparison.OrdinalIgnoreCase));
    }
}
