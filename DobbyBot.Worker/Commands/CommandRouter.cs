using DobbyBot.Worker.Menus;
using DobbyBot.Worker.Services;
using DobbyBot.Worker.Menus;
using DobbyBot.Worker.Modules.Downloader;
using DobbyBot.Worker.Services;
using DobbyBot.Worker.State;

namespace DobbyBot.Worker.Commands;

public sealed class CommandRouter
{
    private readonly IServerStatusService _serverStatusService;
    private readonly IServiceControlService _serviceControlService;
    private readonly IDownloaderInputParser _downloaderInputParser;
    private readonly IUserStateService _userStateService;

    public CommandRouter(
        IServerStatusService serverStatusService,
        IServiceControlService serviceControlService,
        IDownloaderInputParser downloaderInputParser,
        IUserStateService userStateService)
    {
        _serverStatusService = serverStatusService;
        _serviceControlService = serviceControlService;
        _downloaderInputParser = downloaderInputParser;
        _userStateService = userStateService;
    }

    public async Task<BotResponse> HandleAsync(
    long telegramUserId,
    string text,
    CancellationToken cancellationToken)
    {
        var normalizedText = text.Trim();

        if (normalizedText is "/start" or "/menu")
        {
            _userStateService.Clear(telegramUserId);
            return MainMenu();
        }

        if (normalizedText is "/help")
        {
            return Help();
        }

        var state = _userStateService.Get(telegramUserId);

        if (state.Mode == BotUserMode.WaitingForDownloaderInput)
        {
            return HandleDownloaderInput(
                telegramUserId,
                normalizedText);
        }

        return normalizedText switch
        {
            "/status" =>
                new BotResponse(
                    await _serverStatusService.GetStatusAsync(cancellationToken),
                    DobbyMenus.ServerMenu()),

            "/services" =>
                new BotResponse(
                    await _serviceControlService.GetServicesAsync(cancellationToken),
                    DobbyMenus.ServerMenu()),

            _ => await HandleSlashCommand(normalizedText, cancellationToken)
        };
    }

    public async Task<BotResponse> HandleCallbackAsync(
        long telegramUserId,
        string callbackData,
        CancellationToken cancellationToken)
    {
        return callbackData switch
        {
            "menu:main" => ClearAndReturnMainMenu(telegramUserId),
            "menu:server" => ServerMenu(),
            "menu:ai" => AiMenu(),
            "menu:homelab" => HomeLabMenu(),
            "menu:backup" => BackupMenu(),
            "menu:downloader" => DownloaderMenu(),
            "menu:settings" => SettingsMenu(),
            "help" => Help(),

            "downloader:input" => StartDownloaderInputMode(telegramUserId),

            "downloader:platform:instagram" =>
                HandleDownloaderPlatformSelection(
                    telegramUserId,
                    DownloaderSource.Instagram),

            "downloader:platform:telegram" =>
                HandleDownloaderPlatformSelection(
                    telegramUserId,
                    DownloaderSource.Telegram),

            "downloader:cancel" => ClearAndReturnMainMenu(telegramUserId),

            "server:status" =>
                new BotResponse(
                    await _serverStatusService.GetStatusAsync(cancellationToken),
                    DobbyMenus.ServerMenu()),

            "server:services" =>
                new BotResponse(
                    await _serviceControlService.GetServicesAsync(cancellationToken),
                    DobbyMenus.ServerMenu()),

            "server:logs:golotto" =>
                new BotResponse(
                    await _serviceControlService.LogsAsync("golotto", cancellationToken),
                    DobbyMenus.ServerMenu()),

            "server:restart:golotto" =>
                new BotResponse(
                    await _serviceControlService.RestartAsync("golotto", cancellationToken),
                    DobbyMenus.ServerMenu()),

            "ai:ask" =>
                new BotResponse(
                    "🤖 Ask Dobby hələ aktiv deyil. Sonra burada AI chat mode əlavə edəcəyik.",
                    DobbyMenus.AiMenu()),

            "ai:local-status" =>
                new BotResponse(
                    "🧠 Local AI status hələ aktiv deyil. Gələcəkdə Ollama/Qwen/Llama statusunu burada göstərəcəyik.",
                    DobbyMenus.AiMenu()),

            "homelab:docker" =>
                new BotResponse(
                    "🐳 Docker bölməsi hələ aktiv deyil. Sonra container list, logs və restart əlavə edəcəyik.",
                    DobbyMenus.HomeLabMenu()),

            "homelab:nginx" =>
                new BotResponse(
                    "🌐 Nginx bölməsi hələ aktiv deyil. Sonra nginx status, reload və config test əlavə edəcəyik.",
                    DobbyMenus.HomeLabMenu()),

            "homelab:network" =>
                new BotResponse(
                    "📡 Network bölməsi hələ aktiv deyil. Sonra IP, ping, DNS və Tailscale status əlavə edəcəyik.",
                    DobbyMenus.HomeLabMenu()),

            "backup:now" =>
                new BotResponse(
                    "💾 Backup bölməsi hələ aktiv deyil. Sonra PostgreSQL və project backup əlavə edəcəyik.",
                    DobbyMenus.BackupMenu()),

            "backup:last" =>
                new BotResponse(
                    "📦 Backup history hələ aktiv deyil.",
                    DobbyMenus.BackupMenu()),

            "settings:bot-info" =>
                new BotResponse(
                    BotInfo(),
                    DobbyMenus.SettingsMenu()),

            "settings:admin-info" =>
                new BotResponse(
                    "👤 Admin mode aktivdir. Bu bot yalnız icazəli Telegram ID üçün cavab verir.",
                    DobbyMenus.SettingsMenu()),

            _ => new BotResponse(
                "Command tanınmadı. Əsas menyuya qayıtdım.",
                DobbyMenus.MainMenu())
        };
    }

    private BotResponse StartDownloaderInputMode(long telegramUserId)
    {
        _userStateService.SetMode(
            telegramUserId,
            BotUserMode.WaitingForDownloaderInput);

        return new BotResponse(
            """
        📥 Downloader

        Instagram və ya Telegram linki göndər.

        Dəstəklənən nümunələr:
        https://www.instagram.com/p/...
        https://www.instagram.com/reel/...
        https://www.instagram.com/stories/username/...
        https://t.me/channel/123
        https://t.me/username
        @username
        username

        Username göndərsən, Instagram və ya Telegram seçimi çıxacaq.
        """,
            DobbyMenus.DownloaderMenu());
    }

    private BotResponse HandleDownloaderInput(
        long telegramUserId,
        string input)
    {
        var request = _downloaderInputParser.Parse(input);

        if (request.ContentType == DownloaderContentType.Unknown)
        {
            return new BotResponse(
                """
            Link və ya username tanınmadı.

            Instagram/TG linki və ya username göndər.
            """,
                DobbyMenus.DownloaderMenu());
        }

        if (request.RequiresPlatformSelection)
        {
            _userStateService.SetMode(
                telegramUserId,
                BotUserMode.WaitingForDownloaderPlatformSelection);

            _userStateService.SetPendingDownloaderInput(
                telegramUserId,
                request.Value);

            return new BotResponse(
                $"""
            Username tapıldı: {request.Value}

            Bu username hansı platformadandır?
            """,
                DobbyMenus.DownloaderPlatformSelectionMenu());
        }

        _userStateService.Clear(telegramUserId);

        return BuildDownloaderPreviewResponse(request);
    }

    private BotResponse HandleDownloaderPlatformSelection(
        long telegramUserId,
        DownloaderSource source)
    {
        var state = _userStateService.Get(telegramUserId);

        if (string.IsNullOrWhiteSpace(state.PendingDownloaderInput))
        {
            _userStateService.Clear(telegramUserId);

            return new BotResponse(
                "Pending username tapılmadı. Yenidən göndər.",
                DobbyMenus.DownloaderMenu());
        }

        var contentType = source switch
        {
            DownloaderSource.Instagram => DownloaderContentType.InstagramProfile,
            DownloaderSource.Telegram => DownloaderContentType.TelegramChannelOrUser,
            _ => DownloaderContentType.Unknown
        };

        var request = new DownloaderRequest(
            source,
            contentType,
            state.PendingDownloaderInput,
            RequiresPlatformSelection: false);

        _userStateService.Clear(telegramUserId);

        return BuildDownloaderPreviewResponse(request);
    }

    private static BotResponse BuildDownloaderPreviewResponse(
        DownloaderRequest request)
    {
        return new BotResponse(
            $"""
        📥 Downloader request parsed

        Source: {request.Source}
        Type: {request.ContentType}
        Value: {request.Value}

        Real download hələ qoşulmayıb.
        Növbəti mərhələdə bu request uyğun Instagram/Telegram provider-ə yönləndiriləcək.
        """,
            DobbyMenus.DownloaderMenu());
    }

    private BotResponse ClearAndReturnMainMenu(long telegramUserId)
    {
        _userStateService.Clear(telegramUserId);
        return MainMenu();
    }

    private static BotResponse DownloaderMenu()
    {
        return new BotResponse(
            """
        📥 Downloader

        Instagram və ya Telegram linki / username göndər.
        Bot özü linkdən platformanı və kontent tipini tanıyacaq.
        """,
            DobbyMenus.DownloaderMenu());
    }

    private async Task<BotResponse> HandleSlashCommand(
        string text,
        CancellationToken cancellationToken)
    {
        var parts = text.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return MainMenu();
        }

        var command = parts[0].ToLowerInvariant();

        return command switch
        {
            "/restart" when parts.Length == 2 =>
                new BotResponse(
                    await _serviceControlService.RestartAsync(parts[1], cancellationToken),
                    DobbyMenus.ServerMenu()),

            "/logs" when parts.Length == 2 =>
                new BotResponse(
                    await _serviceControlService.LogsAsync(parts[1], cancellationToken),
                    DobbyMenus.ServerMenu()),

            _ => new BotResponse(
                "Command tanınmadı. /menu yaz və ya menudan seçim et.",
                DobbyMenus.MainMenu())
        };
    }

    private static BotResponse MainMenu()
    {
        return new BotResponse(
            """
            🧦 DobbyBot

            Əsas menyudan seçim et:
            """,
            DobbyMenus.MainMenu());
    }

    private static BotResponse ServerMenu()
    {
        return new BotResponse(
            """
            🖥 Server bölməsi

            Buradan server statusunu, service-ləri və GoLotto loglarını idarə edə bilərsən.
            """,
            DobbyMenus.ServerMenu());
    }

    private static BotResponse AiMenu()
    {
        return new BotResponse(
            """
            🤖 AI Assistant bölməsi

            Bu bölmə gələcəkdə local AI/Ollama və ya API əsaslı assistant üçün istifadə olunacaq.
            """,
            DobbyMenus.AiMenu());
    }

    private static BotResponse HomeLabMenu()
    {
        return new BotResponse(
            """
            🧪 HomeLab bölməsi

            Docker, Nginx, network və digər homelab servisləri burada olacaq.
            """,
            DobbyMenus.HomeLabMenu());
    }

    private static BotResponse BackupMenu()
    {
        return new BotResponse(
            """
            💾 Backup bölməsi

            Gələcəkdə database və project backup-ları buradan idarə olunacaq.
            """,
            DobbyMenus.BackupMenu());
    }

    private static BotResponse SettingsMenu()
    {
        return new BotResponse(
            """
            ⚙️ Settings bölməsi

            Bot məlumatları və admin məlumatları burada olacaq.
            """,
            DobbyMenus.SettingsMenu());
    }

    private static BotResponse Help()
    {
        return new BotResponse(
            """
            🧦 DobbyBot Help

            Əsas command-lar:
            /menu
            /status
            /services
            /logs golotto
            /restart golotto

            Daha rahat istifadə üçün inline menudan seçim et.

            Təhlükəsizlik:
            Bot yalnız admin Telegram ID üçün cavab verir.
            Raw terminal command işlətmir.
            Yalnız whitelist edilmiş əmrlər işləyir.
            """,
            DobbyMenus.MainMenu());
    }

    private static string BotInfo()
    {
        return """
        ℹ️ DobbyBot

        Username: @thisisdobby_bot
        Mode: Long polling
        Access: Admin only
        Runtime: .NET Worker Service

        Modules:
        🖥 Server
        🤖 AI Assistant
        🧪 HomeLab
        💾 Backup
        ⚙️ Settings
        """;
    }
}