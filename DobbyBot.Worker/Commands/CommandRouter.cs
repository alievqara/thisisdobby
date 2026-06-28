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

        if (normalizedText.Equals("/start", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Equals("/menu", StringComparison.OrdinalIgnoreCase))
        {
            _userStateService.Clear(telegramUserId);
            return MainMenu();
        }

        if (normalizedText.Equals("/help", StringComparison.OrdinalIgnoreCase))
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

            "/services" or "/containers" =>
                new BotResponse(
                    await _serverStatusService.GetAllContainersAsync(cancellationToken),
                    DobbyMenus.ContainersMenu()),

            "/memory" =>
                new BotResponse(
                    await _serverStatusService.GetMemoryStatusAsync(cancellationToken),
                    DobbyMenus.ServerMenu()),

            "/disk" =>
                new BotResponse(
                    await _serverStatusService.GetDiskStatusAsync(cancellationToken),
                    DobbyMenus.ServerMenu()),

            "/load" =>
                new BotResponse(
                    await _serverStatusService.GetLoadStatusAsync(cancellationToken),
                    DobbyMenus.ServerMenu()),

            _ => await HandleSlashCommand(
                normalizedText,
                cancellationToken)
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
            "menu:containers" => ContainersMenu(),
            "menu:apps" => AppsMenu(),
            "menu:data" => DataLayerMenu(),
            "menu:proxy" => ProxyMenu(),
            "menu:monitoring" => MonitoringMenu(),
            "menu:backup" => BackupMenu(),
            "menu:downloader" => DownloaderMenu(),
            "menu:settings" => SettingsMenu(),
            "help" => Help(),

            "server:status" =>
                new BotResponse(
                    await _serverStatusService.GetStatusAsync(cancellationToken),
                    DobbyMenus.ServerMenu()),

            "server:disk" =>
                new BotResponse(
                    await _serverStatusService.GetDiskStatusAsync(cancellationToken),
                    DobbyMenus.ServerMenu()),

            "server:memory" =>
                new BotResponse(
                    await _serverStatusService.GetMemoryStatusAsync(cancellationToken),
                    DobbyMenus.ServerMenu()),

            "server:load" =>
                new BotResponse(
                    await _serverStatusService.GetLoadStatusAsync(cancellationToken),
                    DobbyMenus.ServerMenu()),

            "server:failed-services" =>
                new BotResponse(
                    await _serverStatusService.GetFailedServicesAsync(cancellationToken),
                    DobbyMenus.ServerMenu()),

            "containers:all" =>
                new BotResponse(
                    await _serverStatusService.GetAllContainersAsync(cancellationToken),
                    DobbyMenus.ContainersMenu()),

            "containers:running" =>
                new BotResponse(
                    await _serverStatusService.GetRunningContainersAsync(cancellationToken),
                    DobbyMenus.ContainersMenu()),

            "containers:failed" =>
                new BotResponse(
                    await _serverStatusService.GetFailedContainersAsync(cancellationToken),
                    DobbyMenus.ContainersMenu()),

            "containers:logs" =>
                new BotResponse(
                    await _serverStatusService.GetContainerLogsSummaryAsync(cancellationToken),
                    DobbyMenus.ContainersMenu()),

            "apps:dobby" => AppDetails(
                "thisisdobby",
                "🧦 This is Dobby"),

            "apps:planzy" => AppDetails(
                "planzy",
                "📅 Planzy"),

            "apps:golotto" => AppDetails(
                "golotto",
                "🎲 GoLotto"),

            "apps:combatfight" => AppDetails(
                "combatfight",
                "🥊 CombatFight"),

            "data:postgres" =>
                new BotResponse(
                    await _serverStatusService.GetPostgresStatusAsync(cancellationToken),
                    DobbyMenus.DataLayerMenu()),

            "data:redis" =>
                new BotResponse(
                    await _serverStatusService.GetRedisStatusAsync(cancellationToken),
                    DobbyMenus.DataLayerMenu()),

            "data:volumes" =>
                new BotResponse(
                    await _serverStatusService.GetVolumesStatusAsync(cancellationToken),
                    DobbyMenus.DataLayerMenu()),

            "data:backups" =>
                new BotResponse(
                    await _serverStatusService.GetDbBackupsStatusAsync(cancellationToken),
                    DobbyMenus.DataLayerMenu()),

            "proxy:npm-status" =>
                new BotResponse(
                    await _serverStatusService.GetProxyStatusAsync(cancellationToken),
                    DobbyMenus.ProxyMenu()),

            "proxy:ssl" =>
                new BotResponse(
                    await _serverStatusService.GetSslStatusAsync(cancellationToken),
                    DobbyMenus.ProxyMenu()),

            "proxy:domains" =>
                NotImplementedOperation(
                    "🧪 Check Domains",
                    """
                    Domain healthcheck üçün ayrıca domain registry əlavə edəcəyik.

                    Gələcək:
                    • planzy.org
                    • alievfaig.dev
                    • subdomain healthcheck
                    • SSL expiry
                    """,
                    DobbyMenus.ProxyMenu()),

            "proxy:network" =>
                NotImplementedOperation(
                    "📡 Proxy Network",
                    "Docker proxy network inspector növbəti mərhələdə əlavə olunacaq.",
                    DobbyMenus.ProxyMenu()),

            "monitoring:summary" =>
                new BotResponse(
                    await _serverStatusService.GetStatusAsync(cancellationToken),
                    DobbyMenus.MonitoringMenu()),

            "monitoring:alerts" =>
                NotImplementedOperation(
                    "🚨 Alerts",
                    "Alert rules və notification history sonra əlavə olunacaq.",
                    DobbyMenus.MonitoringMenu()),

            "monitoring:incidents" =>
                NotImplementedOperation(
                    "🧾 Recent Incidents",
                    "Incident history üçün persistence lazımdır. Sonra PostgreSQL və ya Redis ilə yazılacaq.",
                    DobbyMenus.MonitoringMenu()),

            "backup:list" =>
                new BotResponse(
                    await _serverStatusService.GetDbBackupsStatusAsync(cancellationToken),
                    DobbyMenus.BackupMenu()),

            "backup:verify" =>
                NotImplementedOperation(
                    "🧪 Verify Backup",
                    "Backup verify workflow sonra yazılacaq.",
                    DobbyMenus.BackupMenu()),

            "downloader:telegram-file" =>
                new BotResponse(
                    """
                    📤 Send Telegram File

                    Bot-a photo, video və ya document göndər.

                    Dobby onu serverdə downloads folderinə save edəcək.

                    Qeyd:
                    Bu flow ayrıca Telegram file downloader kimi tamamlanacaq.
                    """,
                    DobbyMenus.DownloaderMenu()),

            "downloader:input" => StartDownloaderInputMode(telegramUserId),

            "downloader:platform:instagram" =>
                HandleDownloaderPlatformSelection(
                    telegramUserId,
                    DownloaderSource.Instagram),

            "downloader:platform:telegram" =>
                HandleDownloaderPlatformSelection(
                    telegramUserId,
                    DownloaderSource.Telegram),

            "downloader:folder" =>
                NotImplementedOperation(
                    "🗂 Downloads Folder",
                    "Downloads folder list sonra əlavə olunacaq.",
                    DobbyMenus.DownloaderMenu()),

            "downloader:cleanup-request" =>
                Confirmation(
                    """
                    🧹 Cleanup Temp

                    Bu əməliyyat temp/download cache təmizləyəcək.

                    Real runner hələ qoşulmayıb.
                    """,
                    confirmCallback: "downloader:cleanup-confirm",
                    cancelCallback: "menu:downloader"),

            "downloader:cleanup-confirm" =>
                NotImplementedOperation(
                    "🧹 Cleanup Temp",
                    "Downloader cleanup runner hələ qoşulmayıb.",
                    DobbyMenus.DownloaderMenu()),

            "downloader:cancel" => ClearAndReturnMainMenu(telegramUserId),

            "settings:admin" =>
                new BotResponse(
                    """
                    👤 Admin

                    Bot admin-only mode-da işləyir.

                    Yalnız konfiqurasiya olunmuş Telegram Admin ID istifadə edə bilər.
                    Non-admin istifadəçilər cavab almır.
                    """,
                    DobbyMenus.SettingsMenu()),

            "settings:security" =>
                new BotResponse(
                    """
                    🔐 Security

                    Prinsiplər:
                    • Dobby root olmamalıdır
                    • unrestricted shell yoxdur
                    • dangerous operation confirmation istəyir
                    • operation-lar audit olunmalıdır
                    """,
                    DobbyMenus.SettingsMenu()),

            "settings:audit" =>
                NotImplementedOperation(
                    "🧾 Audit Logs",
                    "Audit log üçün PostgreSQL persistence əlavə olunacaq.",
                    DobbyMenus.SettingsMenu()),

            "settings:state" =>
                new BotResponse(
                    """
                    🧠 State

                    Hazırda state InMemoryUserStateService ilə saxlanır.

                    Bu o deməkdir:
                    • bot restart olarsa state itir
                    • menu message id itir
                    • pending confirmation itir

                    Production üçün Redis və ya PostgreSQL state backend lazımdır.
                    """,
                    DobbyMenus.SettingsMenu()),

            "settings:about" =>
                new BotResponse(
                    BotInfo(),
                    DobbyMenus.SettingsMenu()),

            _ when callbackData.EndsWith(":status", StringComparison.OrdinalIgnoreCase) &&
                   callbackData.StartsWith("app:", StringComparison.OrdinalIgnoreCase) =>
                AppOperationPlaceholder(callbackData, "📊 Status"),

            _ when callbackData.EndsWith(":logs", StringComparison.OrdinalIgnoreCase) &&
                   callbackData.StartsWith("app:", StringComparison.OrdinalIgnoreCase) =>
                AppOperationPlaceholder(callbackData, "📜 Logs"),

            _ when callbackData.EndsWith(":healthcheck", StringComparison.OrdinalIgnoreCase) &&
                   callbackData.StartsWith("app:", StringComparison.OrdinalIgnoreCase) =>
                AppOperationPlaceholder(callbackData, "🧪 Healthcheck"),

            _ => new BotResponse(
                """
                Command tanınmadı.

                Əsas menyuya qayıtdım.
                """,
                DobbyMenus.MainMenu())
        };
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
                    DobbyMenus.ContainersMenu()),

            "/logs" when parts.Length == 2 =>
                new BotResponse(
                    await _serviceControlService.LogsAsync(parts[1], cancellationToken),
                    DobbyMenus.ContainersMenu()),

            _ => new BotResponse(
                """
                Command tanınmadı.

                /menu yaz və ya menudan seçim et.
                """,
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
            Növbəti mərhələdə bu request uyğun provider-ə yönləndiriləcək.
            """,
            DobbyMenus.DownloaderMenu());
    }

    private BotResponse ClearAndReturnMainMenu(long telegramUserId)
    {
        _userStateService.Clear(telegramUserId);
        return MainMenu();
    }

    private static BotResponse MainMenu()
    {
        return new BotResponse(
            """
            🧦 This is Dobby

            HomeLab Guardian / Telegram DevOps Assistant

            Server, container, app, data layer, proxy, monitoring və backup statuslarını buradan izləyə bilərsən.

            Qeyd:
            Adi text mesajları AI task kimi qəbul olunur.
            """,
            DobbyMenus.MainMenu());
    }

    private static BotResponse ServerMenu()
    {
        return new BotResponse(
            """
            🖥 Server

            Server səviyyəsində status, disk, memory, CPU/load və failed services yoxlamaları.
            """,
            DobbyMenus.ServerMenu());
    }

    private static BotResponse ContainersMenu()
    {
        return new BotResponse(
            """
            🧩 Containers

            Docker container-larının statusu, running/exited siyahısı və log summary bölməsi.
            """,
            DobbyMenus.ContainersMenu());
    }

    private static BotResponse AppsMenu()
    {
        return new BotResponse(
            """
            🚀 Apps

            HomeLab-da idarə etdiyimiz app-lər:

            🧦 This is Dobby
            📅 Planzy
            🎲 GoLotto
            🥊 CombatFight
            """,
            DobbyMenus.AppsMenu());
    }

    private static BotResponse DataLayerMenu()
    {
        return new BotResponse(
            """
            🗄 Data Layer

            Data servis və storage statusları:

            🐘 PostgreSQL
            ⚡ Redis
            💾 Docker Volumes
            🔐 DB Backups
            """,
            DobbyMenus.DataLayerMenu());
    }

    private static BotResponse ProxyMenu()
    {
        return new BotResponse(
            """
            🌐 Proxy & SSL

            Proxy, domain və SSL status bölməsi.
            """,
            DobbyMenus.ProxyMenu());
    }

    private static BotResponse MonitoringMenu()
    {
        return new BotResponse(
            """
            📊 Monitoring

            Health summary, alerts və recent incidents bölməsi.
            """,
            DobbyMenus.MonitoringMenu());
    }

    private static BotResponse BackupMenu()
    {
        return new BotResponse(
            """
            💾 Backup

            Backup list və verify workflow bölməsi.
            """,
            DobbyMenus.BackupMenu());
    }

    private static BotResponse DownloaderMenu()
    {
        return new BotResponse(
            """
            📥 Downloader

            Telegram file, link və username parsing üçün bölmə.
            """,
            DobbyMenus.DownloaderMenu());
    }

    private static BotResponse SettingsMenu()
    {
        return new BotResponse(
            """
            ⚙️ Settings

            Admin, security, audit və state management bölməsi.
            """,
            DobbyMenus.SettingsMenu());
    }

    private static BotResponse AppDetails(
        string appKey,
        string title)
    {
        return new BotResponse(
            $"""
            {title}

            App operation panel.

            App key:
            {appKey}

            Hazırda:
            📊 Status
            📜 Logs
            🧪 Healthcheck

            Restart/update/backup kimi dangerous operation-ları sonra controlled runner ilə qoşacağıq.
            """,
            DobbyMenus.AppDetailsMenu(appKey));
    }

    private static BotResponse AppOperationPlaceholder(
        string callbackData,
        string operationTitle)
    {
        var appKey = ExtractAppKey(callbackData);

        return new BotResponse(
            $"""
            {operationTitle}

            App: {appKey}

            Real app operation runner hələ qoşulmayıb.
            """,
            DobbyMenus.AppDetailsMenu(appKey));
    }

    private static string ExtractAppKey(string callbackData)
    {
        var parts = callbackData.Split(
            ':',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length >= 2
            ? parts[1]
            : "unknown";
    }

    private static BotResponse Confirmation(
        string text,
        string confirmCallback,
        string cancelCallback)
    {
        return new BotResponse(
            text,
            DobbyMenus.ConfirmationMenu(
                confirmCallback,
                cancelCallback));
    }

    private static BotResponse NotImplementedOperation(
        string title,
        string body,
        object replyMarkup)
    {
        return new BotResponse(
            $"""
            {title}

            {body}

            Status:
            Hələ production runner qoşulmayıb.
            """,
            (dynamic)replyMarkup);
    }

    private static BotResponse Help()
    {
        return new BotResponse(
            """
            🧦 This is Dobby Help

            Əsas command-lar:
            /menu
            /status
            /services
            /containers
            /memory
            /disk
            /load
            /logs golotto
            /restart golotto

            AI:
            Adi text mesajı yazanda Dobby onu AI dev task kimi qəbul edir.
            AI üçün ayrıca menu yoxdur.
            """,
            DobbyMenus.MainMenu());
    }

    private static string BotInfo()
    {
        return """
        ℹ️ This is Dobby

        Mode: Long polling
        Runtime: .NET 8 Worker Service
        Access: Admin only

        Role:
        HomeLab Guardian / Telegram DevOps Assistant

        HomeLab:
        Base path: /srv/homelab
        Apps: /srv/homelab/apps
        Compose: /srv/homelab/compose

        Managed areas:
        🖥 Server
        🧩 Containers
        🚀 Apps
        🗄 Data Layer
        🌐 Proxy & SSL
        📊 Monitoring
        💾 Backup
        📥 Downloader

        AI:
        Plain text task mode.
        No separate AI menu.
        """;
    }
}