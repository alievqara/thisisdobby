using DobbyBot.Worker.Commands;
using DobbyBot.Worker.Menus;
using DobbyBot.Worker.Options;
using DobbyBot.Worker.Security;
using DobbyBot.Worker.State;
using Microsoft.Extensions.Options;

namespace DobbyBot.Worker.Bot;

public sealed class DobbyBotWorker : BackgroundService
{
    private readonly TelegramGateway _telegramGateway;
    private readonly CommandRouter _commandRouter;
    private readonly IAdminGuard _adminGuard;
    private readonly IUserStateService _userStateService;
    private readonly DobbyBotOptions _options;
    private readonly ILogger<DobbyBotWorker> _logger;

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

                    await HandleUpdateSafelyAsync(
                        update,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "DobbyBot polling loop failed.");

                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
        }
    }

    private async Task HandleUpdateSafelyAsync(
        TelegramUpdate update,
        CancellationToken cancellationToken)
    {
        try
        {
            await HandleUpdateAsync(
                update,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to handle Telegram update. UpdateId: {UpdateId}",
                update.UpdateId);
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

            // Non-admin user heç bir cavab almır.
            return;
        }

        if (message.Text is null)
        {
            return;
        }

        BotResponse response;

        try
        {
            response = await _commandRouter.HandleAsync(
                message.From.Id,
                message.Text,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to handle text message. Text: {Text}",
                message.Text);

            response = new BotResponse(
                "❌ Xəta baş verdi. Əsas menyuya qayıtdım.",
                DobbyMenus.MainMenu());
        }

        var forceNewMenu = IsMenuResetCommand(message.Text);

        await SendBotResponseAsync(
            message.From.Id,
            message.Chat.Id,
            response,
            forceNewMenu,
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
                "❌ Əməliyyat zamanı xəta baş verdi. Əsas menyuya qayıtdım.",
                DobbyMenus.MainMenu());
        }

        await SendCallbackResponseAsync(
            callbackQuery.From.Id,
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            response,
            cancellationToken);
    }

    private async Task SendBotResponseAsync(
        long telegramUserId,
        long chatId,
        BotResponse response,
        bool forceNewMenu,
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

        if (forceNewMenu)
        {
            if (state.MenuMessageId is not null)
            {
                await _telegramGateway.DeleteMessageAsync(
                    chatId,
                    state.MenuMessageId.Value,
                    cancellationToken);
            }

            _userStateService.ClearMenuMessageId(telegramUserId);

            var newMenuMessage = await _telegramGateway.SendMessageAsync(
                chatId,
                response.Text,
                response.ReplyMarkup,
                cancellationToken);

            if (newMenuMessage is not null)
            {
                _userStateService.SetMenuMessageId(
                    telegramUserId,
                    newMenuMessage.MessageId);
            }

            return;
        }

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

            _userStateService.ClearMenuMessageId(telegramUserId);
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

    private async Task SendCallbackResponseAsync(
        long telegramUserId,
        long chatId,
        long callbackMessageId,
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

        if (state.MenuMessageId is not null &&
            state.MenuMessageId.Value != callbackMessageId)
        {
            await _telegramGateway.DeleteMessageAsync(
                chatId,
                callbackMessageId,
                cancellationToken);

            await SendBotResponseAsync(
                telegramUserId,
                chatId,
                response,
                forceNewMenu: false,
                cancellationToken);

            return;
        }

        var edited = await _telegramGateway.EditMessageTextAsync(
            chatId,
            callbackMessageId,
            response.Text,
            response.ReplyMarkup,
            cancellationToken);

        if (edited)
        {
            _userStateService.SetMenuMessageId(
                telegramUserId,
                callbackMessageId);

            return;
        }

        await _telegramGateway.DeleteMessageAsync(
            chatId,
            callbackMessageId,
            cancellationToken);

        _userStateService.ClearMenuMessageId(telegramUserId);

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
        await _telegramGateway.DeleteMessageAsync(
            message.Chat.Id,
            message.MessageId,
            cancellationToken);
    }

    private static bool IsMenuResetCommand(string text)
    {
        var normalizedText = text.Trim();

        return normalizedText.Equals(
                   "/start",
                   StringComparison.OrdinalIgnoreCase)
               || normalizedText.Equals(
                   "/menu",
                   StringComparison.OrdinalIgnoreCase);
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