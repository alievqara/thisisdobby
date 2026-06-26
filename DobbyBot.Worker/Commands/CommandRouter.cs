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

            "/services" =>
                new BotResponse(
                    await _serviceControlService.GetServicesAsync(cancellationToken),
                    DobbyMenus.ContainersMenu()),

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
            // Main navigation
            "menu:main" => ClearAndReturnMainMenu(telegramUserId),
            "menu:server" => ServerMenu(),
            "menu:containers" => ContainersMenu(),
            "menu:apps" => AppsMenu(),
            "menu:data" => DataLayerMenu(),
            "menu:proxy" => ProxyMenu(),
            "menu:monitoring" => MonitoringMenu(),
            "menu:backup" => BackupMenu(),
            "menu:downloader" => DownloaderMenu(),
            "menu:ai" => AiMenu(),
            "menu:settings" => SettingsMenu(),
            "help" => Help(),

            // Server callbacks
            "server:status" =>
                new BotResponse(
                    await _serverStatusService.GetStatusAsync(cancellationToken),
                    DobbyMenus.ServerMenu()),

            "server:disk" =>
                Placeholder(
                    "💽 Disk",
                    """
                    Disk monitor hələ tam ayrılmayıb.

                    Gələcəkdə burada:
                    - root disk usage
                    - Docker volumes
                    - backup folder size
                    - low disk warning
                    göstəriləcək.
                    """,
                    DobbyMenus.ServerMenu()),

            "server:memory" =>
                Placeholder(
                    "🧠 Memory",
                    """
                    Memory monitor hələ tam ayrılmayıb.

                    Gələcəkdə burada:
                    - RAM usage
                    - swap usage
                    - top memory containers
                    göstəriləcək.
                    """,
                    DobbyMenus.ServerMenu()),

            "server:load" =>
                Placeholder(
                    "🔥 CPU / Load",
                    """
                    CPU/load monitor hələ tam ayrılmayıb.

                    Gələcəkdə burada:
                    - uptime/load average
                    - CPU pressure
                    - top CPU containers
                    göstəriləcək.
                    """,
                    DobbyMenus.ServerMenu()),

            "server:failed-services" =>
                Placeholder(
                    "🧯 Failed Services",
                    """
                    Failed services yoxlaması hələ Docker/HomeLab modelinə keçirilməyib.

                    Bare-metal systemctl yox, controlled script/operation modeli ilə yazılacaq.
                    """,
                    DobbyMenus.ServerMenu()),

            "server:reboot-request" =>
                Confirmation(
                    """
                    🔁 Reboot Request

                    Bu dangerous operation sayılır.

                    Production modeldə:
                    1. active deploy/backup yoxlanacaq
                    2. confirmation istənəcək
                    3. audit log yazılacaq
                    4. reboot controlled script ilə icra olunacaq
                    """,
                    confirmCallback: "server:reboot-confirm",
                    cancelCallback: "menu:server"),

            "server:reboot-confirm" =>
                NotImplementedOperation(
                    "🔁 Reboot",
                    "Server reboot operation hələ qoşulmayıb. Bu əməliyyat production-da confirmation + audit + controlled script ilə işləyəcək.",
                    DobbyMenus.ServerMenu()),

            // Containers callbacks
            "containers:all" =>
                new BotResponse(
                    await _serviceControlService.GetServicesAsync(cancellationToken),
                    DobbyMenus.ContainersMenu()),

            "containers:running" =>
                NotImplementedOperation(
                    "✅ Running Containers",
                    "Docker container status hələ systemctl modelindən ayrılmayıb. Növbəti mərhələdə script-based container status yazacağıq.",
                    DobbyMenus.ContainersMenu()),

            "containers:failed" =>
                NotImplementedOperation(
                    "❌ Failed / Exited Containers",
                    "Exited/failed container yoxlaması controlled Docker operation ilə yazılacaq.",
                    DobbyMenus.ContainersMenu()),

            "containers:logs" =>
                NotImplementedOperation(
                    "📜 Container Logs",
                    "Container logs üçün əvvəl app/container seçimi əlavə olunmalıdır.",
                    DobbyMenus.ContainersMenu()),

            "containers:restart" =>
                Confirmation(
                    """
                    🔄 Restart Container

                    Bu dangerous operation sayılır.

                    Növbəti mərhələdə:
                    - container/app seçimi
                    - confirmation
                    - audit log
                    - controlled restart script
                    əlavə ediləcək.
                    """,
                    confirmCallback: "containers:restart-confirm",
                    cancelCallback: "menu:containers"),

            "containers:restart-confirm" =>
                NotImplementedOperation(
                    "🔄 Restart Container",
                    "Restart container operation hələ qoşulmayıb.",
                    DobbyMenus.ContainersMenu()),

            // Apps
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

            "apps:add" =>
                new BotResponse(
                    """
                    ➕ Add App

                    Bu feature sonra gələcək.

                    Gələcək app modeli:
                    - app key
                    - compose path
                    - source path
                    - db name
                    - healthcheck url
                    - backup policy
                    - deploy script
                    """,
                    DobbyMenus.AppsMenu()),

            // Data layer
            "data:postgres" =>
                NotImplementedOperation(
                    "🐘 PostgreSQL",
                    """
                    PostgreSQL panel sonra gələcək.

                    Hazır container:
                    dobby-postgres

                    Gələcək:
                    - DB list
                    - connection status
                    - backup status
                    - per-app DB user check
                    """,
                    DobbyMenus.DataLayerMenu()),

            "data:redis" =>
                NotImplementedOperation(
                    "⚡ Redis",
                    """
                    Redis panel sonra gələcək.

                    Hazır container:
                    dobby-redis

                    Gələcək:
                    - ping
                    - memory usage
                    - key count
                    - Dobby state backend
                    """,
                    DobbyMenus.DataLayerMenu()),

            "data:volumes" =>
                NotImplementedOperation(
                    "💾 Volumes",
                    "Docker volumes və data folders üçün status panel sonra yazılacaq.",
                    DobbyMenus.DataLayerMenu()),

            "data:backups" =>
                NotImplementedOperation(
                    "🔐 DB Backups",
                    "Database backup list və verify workflow sonra yazılacaq.",
                    DobbyMenus.DataLayerMenu()),

            // Proxy & SSL
            "proxy:npm-status" =>
                NotImplementedOperation(
                    "🌍 Nginx Proxy Manager",
                    """
                    NPM status sonra gələcək.

                    Hazır container:
                    dobby-npm

                    Gələcək:
                    - container status
                    - proxy host count
                    - SSL status
                    - domain checks
                    """,
                    DobbyMenus.ProxyMenu()),

            "proxy:ssl" =>
                NotImplementedOperation(
                    "🔐 SSL Certificates",
                    "SSL certificate check sonra əlavə olunacaq.",
                    DobbyMenus.ProxyMenu()),

            "proxy:domains" =>
                NotImplementedOperation(
                    "🧪 Check Domains",
                    "Domain healthcheck və SSL expiry check sonra əlavə olunacaq.",
                    DobbyMenus.ProxyMenu()),

            "proxy:network" =>
                NotImplementedOperation(
                    "📡 Proxy Network",
                    "dobby_proxy network status sonra əlavə olunacaq.",
                    DobbyMenus.ProxyMenu()),

            // Monitoring
            "monitoring:kuma" =>
                NotImplementedOperation(
                    "💚 Uptime Kuma",
                    """
                    Uptime Kuma panel sonra gələcək.

                    Hazır container:
                    dobby-uptime-kuma

                    Gələcək:
                    - monitor summary
                    - down services
                    - recent incidents
                    """,
                    DobbyMenus.MonitoringMenu()),

            "monitoring:summary" =>
                NotImplementedOperation(
                    "📈 Health Summary",
                    "HomeLab health summary sonra yazılacaq.",
                    DobbyMenus.MonitoringMenu()),

            "monitoring:alerts" =>
                NotImplementedOperation(
                    "🚨 Alerts",
                    "Alert list və notification rules sonra əlavə olunacaq.",
                    DobbyMenus.MonitoringMenu()),

            "monitoring:incidents" =>
                NotImplementedOperation(
                    "🧾 Recent Incidents",
                    "Incident history üçün persistence lazımdır. Sonra PostgreSQL ilə yazılacaq.",
                    DobbyMenus.MonitoringMenu()),

            // Backup
            "backup:planzy-db-request" =>
                Confirmation(
                    """
                    📦 Backup Planzy DB

                    Bu əməliyyat DB backup yaradacaq.

                    Production workflow:
                    1. DB connection yoxla
                    2. pg_dump al
                    3. backup folderə yaz
                    4. checksum yarat
                    5. result logla
                    """,
                    confirmCallback: "backup:planzy-db-confirm",
                    cancelCallback: "menu:backup"),

            "backup:golotto-db-request" =>
                Confirmation(
                    """
                    📦 Backup GoLotto DB

                    Bu əməliyyat GoLotto DB backup yaradacaq.
                    Hələ real runner qoşulmayıb.
                    """,
                    confirmCallback: "backup:golotto-db-confirm",
                    cancelCallback: "menu:backup"),

            "backup:dobby-config-request" =>
                Confirmation(
                    """
                    📦 Backup Dobby Config

                    Bu əməliyyat Dobby config/secrets olmayan runtime config backup üçün istifadə olunacaq.
                    Real secrets backup edilməməlidir.
                    """,
                    confirmCallback: "backup:dobby-config-confirm",
                    cancelCallback: "menu:backup"),

            "backup:planzy-db-confirm" =>
                NotImplementedOperation(
                    "📦 Backup Planzy DB",
                    "Planzy DB backup runner hələ qoşulmayıb.",
                    DobbyMenus.BackupMenu()),

            "backup:golotto-db-confirm" =>
                NotImplementedOperation(
                    "📦 Backup GoLotto DB",
                    "GoLotto DB backup runner hələ qoşulmayıb.",
                    DobbyMenus.BackupMenu()),

            "backup:dobby-config-confirm" =>
                NotImplementedOperation(
                    "📦 Backup Dobby Config",
                    "Dobby config backup runner hələ qoşulmayıb.",
                    DobbyMenus.BackupMenu()),

            "backup:list" =>
                NotImplementedOperation(
                    "🗂 Backup List",
                    "Backup list üçün storage folder scan və metadata lazımdır. Sonra yazılacaq.",
                    DobbyMenus.BackupMenu()),

            "backup:verify" =>
                NotImplementedOperation(
                    "🧪 Verify Backup",
                    "Backup verify workflow sonra yazılacaq.",
                    DobbyMenus.BackupMenu()),

            // Downloader
            "downloader:telegram-file" =>
                new BotResponse(
                    """
                    📤 Send Telegram File

                    İstifadə qaydası:
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

            // AI
            "ai:ask" =>
                new BotResponse(
                    """
                    🧠 Ask Dobby

                    AI chat mode hələ aktiv deyil.

                    Gələcəkdə:
                    - local Ollama
                    - log explanation
                    - command recommendation
                    - incident diagnosis
                    qoşula bilər.
                    """,
                    DobbyMenus.AiMenu()),

            "ai:explain-logs" =>
                new BotResponse(
                    """
                    📋 Explain Logs

                    Log izahı üçün əvvəl log source seçimi lazımdır:
                    - Planzy
                    - GoLotto
                    - Dobby
                    - NPM
                    - PostgreSQL

                    AI modulu sonra qoşulacaq.
                    """,
                    DobbyMenus.AiMenu()),

            "ai:diagnose" =>
                new BotResponse(
                    """
                    🧪 Diagnose Error

                    Dobby gələcəkdə error loglarını oxuyub səbəb və fix təklif edəcək.
                    Hələ aktiv deyil.
                    """,
                    DobbyMenus.AiMenu()),

            "ai:suggest-fix" =>
                new BotResponse(
                    """
                    🛠 Suggest Fix

                    Bu feature sonra gələcək.

                    Qayda:
                    Dobby heç vaxt fix-i avtomatik tətbiq etməməlidir.
                    Əvvəl izah, sonra confirmation.
                    """,
                    DobbyMenus.AiMenu()),

            // Settings
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
                    - Dobby root olmamalıdır
                    - unrestricted shell yoxdur
                    - Docker socket default mount olunmur
                    - dangerous operation confirmation istəyir
                    - operation-lar audit olunmalıdır
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
                    - bot restart olarsa state itir
                    - menu message id itir
                    - pending confirmation itir

                    Production üçün Redis və ya PostgreSQL state backend lazımdır.
                    """,
                    DobbyMenus.SettingsMenu()),

            "settings:about" =>
                new BotResponse(
                    BotInfo(),
                    DobbyMenus.SettingsMenu()),

            // Generic app operations
            _ when callbackData.EndsWith(":status", StringComparison.OrdinalIgnoreCase) &&
                   callbackData.StartsWith("app:", StringComparison.OrdinalIgnoreCase) =>
                AppOperationPlaceholder(callbackData, "📊 Status"),

            _ when callbackData.EndsWith(":logs", StringComparison.OrdinalIgnoreCase) &&
                   callbackData.StartsWith("app:", StringComparison.OrdinalIgnoreCase) =>
                AppOperationPlaceholder(callbackData, "📜 Logs"),

            _ when callbackData.EndsWith(":healthcheck", StringComparison.OrdinalIgnoreCase) &&
                   callbackData.StartsWith("app:", StringComparison.OrdinalIgnoreCase) =>
                AppOperationPlaceholder(callbackData, "🧪 Healthcheck"),

            _ when callbackData.EndsWith(":restart-request", StringComparison.OrdinalIgnoreCase) &&
                   callbackData.StartsWith("app:", StringComparison.OrdinalIgnoreCase) =>
                Confirmation(
                    """
                    🔄 Restart request

                    Bu dangerous operation sayılır.

                    Production model:
                    1. app status yoxlanır
                    2. confirmation alınır
                    3. restart script işləyir
                    4. healthcheck gözlənir
                    5. result audit log-a yazılır
                    """,
                    confirmCallback: callbackData.Replace("-request", "-confirm", StringComparison.OrdinalIgnoreCase),
                    cancelCallback: "menu:apps"),

            _ when callbackData.EndsWith(":update-request", StringComparison.OrdinalIgnoreCase) &&
                   callbackData.StartsWith("app:", StringComparison.OrdinalIgnoreCase) =>
                Confirmation(
                    """
                    ⬆️ Update request

                    Production update workflow:
                    1. status yoxla
                    2. backup al
                    3. build/image hazırla
                    4. deploy et
                    5. healthcheck gözlə
                    6. fail olsa rollback et
                    """,
                    confirmCallback: callbackData.Replace("-request", "-confirm", StringComparison.OrdinalIgnoreCase),
                    cancelCallback: "menu:apps"),

            _ when callbackData.EndsWith(":backup-db-request", StringComparison.OrdinalIgnoreCase) &&
                   callbackData.StartsWith("app:", StringComparison.OrdinalIgnoreCase) =>
                Confirmation(
                    """
                    💾 Backup DB request

                    Bu əməliyyat DB backup üçün istifadə olunacaq.

                    Qayda:
                    Backup operation mütləq audit log-a yazılmalıdır.
                    """,
                    confirmCallback: callbackData.Replace("-request", "-confirm", StringComparison.OrdinalIgnoreCase),
                    cancelCallback: "menu:apps"),

            _ when callbackData.EndsWith("-confirm", StringComparison.OrdinalIgnoreCase) =>
                NotImplementedOperation(
                    "✅ Confirm received",
                    """
                    Confirmation qəbul edildi.

                    Real operation runner hələ qoşulmayıb.
                    Bu mərhələdə yalnız menu və workflow stabilizasiya edirik.
                    """,
                    DobbyMenus.MainMenu()),

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

            Mən sənin HomeLab-ını izləmək, idarə etmək və təhlükəsiz operation-ları icra etmək üçün buradayam.
            """,
            DobbyMenus.MainMenu());
    }

    private static BotResponse ServerMenu()
    {
        return new BotResponse(
            """
            🖥 Server

            Server səviyyəsində status, disk, memory, load və failed service yoxlamaları.

            Dangerous operation-lar confirmation istəyəcək.
            """,
            DobbyMenus.ServerMenu());
    }

    private static BotResponse ContainersMenu()
    {
        return new BotResponse(
            """
            🧩 Containers

            HomeLab Docker container-larının statusu, logs və restart əməliyyatları.

            Qayda:
            Docker socket birbaşa açılmayacaq.
            Controlled operation/script modeli istifadə olunacaq.
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

            Hər app üçün status, logs, restart, update, backup və healthcheck workflow olacaq.
            """,
            DobbyMenus.AppsMenu());
    }

    private static BotResponse DataLayerMenu()
    {
        return new BotResponse(
            """
            🗄 Data Layer

            Hazır data layer:

            🐘 PostgreSQL: dobby-postgres
            ⚡ Redis: dobby-redis

            Gələcəkdə DB status, backup, restore və Redis state burada idarə olunacaq.
            """,
            DobbyMenus.DataLayerMenu());
    }

    private static BotResponse ProxyMenu()
    {
        return new BotResponse(
            """
            🌐 Proxy & SSL

            Hazır proxy layer:

            🌍 Nginx Proxy Manager: dobby-npm
            📡 Network: dobby_proxy

            SSL certificate, domain healthcheck və proxy status burada olacaq.
            """,
            DobbyMenus.ProxyMenu());
    }

    private static BotResponse MonitoringMenu()
    {
        return new BotResponse(
            """
            📊 Monitoring

            Hazır monitoring:

            💚 Uptime Kuma: dobby-uptime-kuma

            Health summary, alerts və recent incidents burada olacaq.
            """,
            DobbyMenus.MonitoringMenu());
    }

    private static BotResponse BackupMenu()
    {
        return new BotResponse(
            """
            💾 Backup

            Backup workflow:

            - app database backup
            - Dobby config backup
            - backup list
            - backup verify

            Real runner sonra controlled scripts ilə qoşulacaq.
            """,
            DobbyMenus.BackupMenu());
    }

    private static BotResponse DownloaderMenu()
    {
        return new BotResponse(
            """
            📥 Downloader

            Telegram file, link və username parsing üçün bölmə.

            Qayda:
            Public/authorized content xaricində private bypass etməyəcəyik.
            """,
            DobbyMenus.DownloaderMenu());
    }

    private static BotResponse AiMenu()
    {
        return new BotResponse(
            """
            🤖 AI Assistant

            Gələcək Dobby AI modulu:

            🧠 Ask Dobby
            📋 Explain Logs
            🧪 Diagnose Error
            🛠 Suggest Fix

            Qayda:
            AI heç vaxt avtomatik dangerous operation icra etməməlidir.
            """,
            DobbyMenus.AiMenu());
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

            Planned workflow:
            📊 Status
            📜 Logs
            🔄 Restart
            ⬆️ Update
            💾 Backup DB
            🧪 Healthcheck

            App key:
            {appKey}

            Safety:
            Restart, update və backup kimi əməliyyatlar controlled operation və confirmation ilə işləyəcək.
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

            Real operation runner hələ qoşulmayıb.

            Gələcək model:
            - operation request yaradılır
            - permission yoxlanır
            - risk level yoxlanır
            - confirmation lazımdırsa istənir
            - script/runner işləyir
            - audit log yazılır
            - nəticə Telegram-a göndərilir
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

    private static BotResponse Placeholder(
        string title,
        string body,
        object replyMarkup)
    {
        return new BotResponse(
            $"""
            {title}

            {body}
            """,
            (dynamic)replyMarkup);
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
            /logs golotto
            /restart golotto

            Yeni məqsəd:
            Dobby HomeLab Guardian / Telegram DevOps Assistant-dır.

            Təhlükəsizlik:
            - Bot yalnız admin Telegram ID üçün cavab verir
            - Raw terminal yoxdur
            - Root bot olmayacaq
            - Docker socket default açılmayacaq
            - Dangerous operation confirmation istəyəcək
            - Operation-lar audit olunacaq
            """,
            DobbyMenus.MainMenu());
    }

    private static string BotInfo()
    {
        return """
        ℹ️ This is Dobby

        Username: @thisisdobby_bot
        Mode: Long polling
        Runtime: .NET 8 Worker Service
        Access: Admin only

        Role:
        HomeLab Guardian / Telegram DevOps Assistant

        HomeLab:
        Base path: /srv/homelab
        Apps: /srv/homelab/apps
        Compose: /srv/homelab/compose

        Containers:
        dobby-postgres
        dobby-redis
        dobby-npm
        dobby-uptime-kuma
        dobby-portainer

        Networks:
        dobby_proxy
        dobby_internal
        dobby_data
        """;
    }
}