using DobbyBot.Worker.Commands;
using DobbyBot.Worker.Options;
using DobbyBot.Worker.Security;
using Microsoft.Extensions.Options;

namespace DobbyBot.Worker.Bot;

public sealed class DobbyBotWorker : BackgroundService
{
    private readonly TelegramGateway _telegramGateway;
    private readonly CommandRouter _commandRouter;
    private readonly IAdminGuard _adminGuard;
    private readonly DobbyBotOptions _options;
    private readonly ILogger<DobbyBotWorker> _logger;

    public DobbyBotWorker(
        TelegramGateway telegramGateway,
        CommandRouter commandRouter,
        IAdminGuard adminGuard,
        IOptions<DobbyBotOptions> options,
        ILogger<DobbyBotWorker> logger)
    {
        _telegramGateway = telegramGateway;
        _commandRouter = commandRouter;
        _adminGuard = adminGuard;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateOptions();

        long offset = 0;

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

                    await HandleUpdateAsync(update, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DobbyBot polling failed.");

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleUpdateAsync(
        TelegramUpdate update,
        CancellationToken cancellationToken)
    {
        if (update.Message is not null)
        {
            await HandleMessageAsync(update.Message, cancellationToken);
            return;
        }

        if (update.CallbackQuery is not null)
        {
            await HandleCallbackQueryAsync(update.CallbackQuery, cancellationToken);
        }
    }

    private async Task HandleMessageAsync(
        TelegramMessage message,
        CancellationToken cancellationToken)
    {
        if (message.Text is null || message.From is null)
        {
            return;
        }

        if (!_adminGuard.IsAdmin(message.From.Id))
        {
            _logger.LogWarning(
                "Unauthorized Telegram message. UserId: {UserId}, Username: {Username}, Text: {Text}",
                message.From.Id,
                message.From.Username,
                message.Text);

            await _telegramGateway.SendMessageAsync(
                message.Chat.Id,
                UnauthorizedAccessMessage(),
                null,
                cancellationToken);

            return;
        }

        var response = await _commandRouter.HandleAsync(
            message.From.Id,
            message.Text,
            cancellationToken);

        await _telegramGateway.SendMessageAsync(
            message.Chat.Id,
            response.Text,
            response.ReplyMarkup,
            cancellationToken);
    }

    private async Task HandleCallbackQueryAsync(
        TelegramCallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        if (!_adminGuard.IsAdmin(callbackQuery.From.Id))
        {
            _logger.LogWarning(
                "Unauthorized Telegram callback. UserId: {UserId}, Username: {Username}, Data: {Data}",
                callbackQuery.From.Id,
                callbackQuery.From.Username,
                callbackQuery.Data);

            if (callbackQuery.Message is not null)
            {
                await _telegramGateway.SendMessageAsync(
                    callbackQuery.Message.Chat.Id,
                    UnauthorizedAccessMessage(),
                    null,
                    cancellationToken);
            }

            return;
        }

        if (callbackQuery.Message is null || callbackQuery.Data is null)
        {
            return;
        }

        await _telegramGateway.AnswerCallbackQueryAsync(
            callbackQuery.Id,
            cancellationToken);

        var response = await _commandRouter.HandleCallbackAsync(
            callbackQuery.From.Id,
            callbackQuery.Data,
            cancellationToken);

        await _telegramGateway.EditMessageTextAsync(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            response.Text,
            response.ReplyMarkup,
            cancellationToken);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            throw new InvalidOperationException("DobbyBot token is missing.");
        }

        if (_options.AdminTelegramId <= 0)
        {
            throw new InvalidOperationException("DobbyBot admin Telegram ID is missing.");
        }
    }


    private static string UnauthorizedAccessMessage()
    {
        return """
        Access denied.

        This is a private personal bot. It is not a public service and cannot be used by unauthorized users.

        For security reasons, this access attempt may be logged, including your Telegram user ID, username and message metadata.

        Please do not continue interacting with this bot. Harmful, abusive or suspicious activity may be handled through appropriate legal channels.
        """;
    }
}