using DobbyBot.Worker.Commands;
using DobbyBot.Worker.Menus;
using DobbyBot.Worker.Options;
using DobbyBot.Worker.Security;
using DobbyBot.Worker.State;
using Microsoft.Extensions.Options;
using DobbyBot.Worker.Menus;

namespace DobbyBot.Worker.Bot;

public sealed class DobbyBotWorker : BackgroundService
{
    private readonly TelegramGateway _telegramGateway;
    private readonly CommandRouter _commandRouter;
    private readonly IAdminGuard _adminGuard;
    private readonly DobbyBotOptions _options;
    private readonly ILogger<DobbyBotWorker> _logger;
    private readonly IUserStateService _userStateService;

    public DobbyBotWorker(
        TelegramGateway telegramGateway,
        CommandRouter commandRouter,
        IAdminGuard adminGuard,
        IUserStateService userStateService,
        IOptions<DobbyBotOptions> options,
        ILogger<DobbyBotWorker> logger)
    {
        _telegramGateway = telegramGateway;
        _commandRouter = commandRouter;
        _adminGuard = adminGuard;
        _userStateService = userStateService;
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
        if (message.From is null)
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
                "Private bot. Access denied.",
                null,
                cancellationToken);

            await TryDeleteUserMessageAsync(
                message,
                cancellationToken);

            return;
        }

        if (message.Text is null)
        {
            return;
        }

        var response = await _commandRouter.HandleAsync(
            message.From.Id,
            message.Text,
            cancellationToken);

        await SendBotResponseAsync(
            message.From.Id,
            message.Chat.Id,
            response,
            cancellationToken);

        await TryDeleteUserMessageAsync(
            message,
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

            await _telegramGateway.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                cancellationToken);

            return;
        }

        if (callbackQuery.Message is null || callbackQuery.Data is null)
        {
            await _telegramGateway.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                cancellationToken);

            return;
        }

        await _telegramGateway.AnswerCallbackQueryAsync(
            callbackQuery.Id,
            cancellationToken);

        BotResponse response;

        try
        {
            response = await _commandRouter.HandleCallbackAsync(
                callbackQuery.From.Id,
                callbackQuery.Data,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to handle callback. Data: {Data}",
                callbackQuery.Data);

            response = new BotResponse(
                "❌ Əməliyyat zamanı xəta baş verdi. Əsas menyuya qayıt.",
                DobbyMenus.MainMenu());
        }

        var edited = await _telegramGateway.EditMessageTextAsync(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            response.Text,
            response.ReplyMarkup,
            cancellationToken);

        if (edited)
        {
            _userStateService.SetMenuMessageId(
                callbackQuery.From.Id,
                callbackQuery.Message.MessageId);

            return;
        }

        var sentMessage = await _telegramGateway.SendMessageAsync(
            callbackQuery.Message.Chat.Id,
            response.Text,
            response.ReplyMarkup,
            cancellationToken);

        if (sentMessage is not null)
        {
            _userStateService.SetMenuMessageId(
                callbackQuery.From.Id,
                sentMessage.MessageId);
        }
    }

    private async Task SendBotResponseAsync(
    long telegramUserId,
    long chatId,
    BotResponse response,
    CancellationToken cancellationToken)
    {
        if (response.ReplyMarkup is null)
        {
            await _telegramGateway.SendMessageAsync(
                chatId,
                response.Text,
                null,
                cancellationToken);

            return;
        }

        var state = _userStateService.Get(telegramUserId);

        if (state.MenuMessageId is not null)
        {
            var edited = await _telegramGateway.EditMessageTextAsync(
                chatId,
                state.MenuMessageId.Value,
                response.Text,
                response.ReplyMarkup,
                cancellationToken);

            if (edited)
            {
                return;
            }

            await _telegramGateway.DeleteMessageAsync(
                chatId,
                state.MenuMessageId.Value,
                cancellationToken);
        }

        var sentMessage = await _telegramGateway.SendMessageAsync(
            chatId,
            response.Text,
            response.ReplyMarkup,
            cancellationToken);

        if (sentMessage is not null)
        {
            _userStateService.SetMenuMessageId(
                telegramUserId,
                sentMessage.MessageId);
        }
    }

    private async Task TryDeleteUserMessageAsync(
        TelegramMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await _telegramGateway.DeleteMessageAsync(
                message.Chat.Id,
                message.MessageId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Failed to delete user message. MessageId: {MessageId}",
                message.MessageId);
        }
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
}