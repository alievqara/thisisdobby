using System.Text;
using DobbyBot.Worker.Menus;
using DobbyBot.Worker.Modules.Downloader;
using DobbyBot.Worker.Services;
using DobbyBot.Worker.Services.Docker;
using DobbyBot.Worker.Services.SystemPower;
using DobbyBot.Worker.State;

namespace DobbyBot.Worker.Commands;

public sealed class CommandRouter
{
    private readonly IServerStatusService _serverStatusService;
    private readonly IServiceControlService _serviceControlService;
    private readonly IDownloaderInputParser _downloaderInputParser;
    private readonly IUserStateService _userStateService;
    private readonly IDockerService _dockerService;
    private readonly ISystemPowerService _systemPowerService;

    public CommandRouter(
        IServerStatusService serverStatusService,
        IServiceControlService serviceControlService,
        IDownloaderInputParser downloaderInputParser,
        IUserStateService userStateService,
        IDockerService dockerService,
        ISystemPowerService systemPowerService)
    {
        _serverStatusService = serverStatusService;
        _serviceControlService = serviceControlService;
        _downloaderInputParser = downloaderInputParser;
        _userStateService = userStateService;
        _dockerService = dockerService;
        _systemPowerService = systemPowerService;
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
                    DobbyMenus.HomeLabMenu()),

            "/docker" or "/containers" or "/services" =>
                await DockerGroups(cancellationToken),

            "/memory" =>
                new BotResponse(
                    await _serverStatusService.GetMemoryStatusAsync(cancellationToken),
                    DobbyMenus.HomeLabMenu()),

            "/disk" =>
                new BotResponse(
                    await _serverStatusService.GetDiskStatusAsync(cancellationToken),
                    DobbyMenus.HomeLabMenu()),

            "/load" =>
                new BotResponse(
                    await _serverStatusService.GetLoadStatusAsync(cancellationToken),
                    DobbyMenus.HomeLabMenu()),

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
            "menu:homelab" => HomeLabMenu(),
            "menu:docker" => await DockerGroups(cancellationToken),
            "menu:apps" => AppsMenu(),
            "menu:data" => DataLayerMenu(),
            "menu:proxy" => ProxyMenu(),
            "menu:backup" => BackupMenu(),
            "menu:downloader" => DownloaderMenu(),
            "menu:settings" => SettingsMenu(),
            "help" => Help(),

            "homelab:status" =>
                new BotResponse(
                    await _serverStatusService.GetStatusAsync(cancellationToken),
                    DobbyMenus.HomeLabMenu()),

            "homelab:disk" =>
                new BotResponse(
                    await _serverStatusService.GetDiskStatusAsync(cancellationToken),
                    DobbyMenus.HomeLabMenu()),

            "homelab:memory" =>
                new BotResponse(
                    await _serverStatusService.GetMemoryStatusAsync(cancellationToken),
                    DobbyMenus.HomeLabMenu()),

            "homelab:load" =>
                new BotResponse(
                    await _serverStatusService.GetLoadStatusAsync(cancellationToken),
                    DobbyMenus.HomeLabMenu()),

            "homelab:failed-services" =>
                new BotResponse(
                    await _serverStatusService.GetFailedServicesAsync(cancellationToken),
                    DobbyMenus.HomeLabMenu()),

            _ when callbackData.StartsWith("docker:group:", StringComparison.OrdinalIgnoreCase) =>
                await DockerGroup(callbackData, cancellationToken),

            _ when callbackData.StartsWith("docker:container:", StringComparison.OrdinalIgnoreCase) =>
                await DockerContainer(callbackData, cancellationToken),

            "apps:dobby" => DobbyAppDetails(),

            "apps:dobby-ai" =>
                new BotResponse(
                    """
🤖 Dobby AI

Status:
Not connected yet.

Future:
• Claude Code
• Ollama
• Open WebUI
• Local model stack
""",
                    DobbyMenus.DobbyAppMenu()),

            "apps:planzy" => AppDetails(
                "planzy",
                "📅 Planzy"),

            "apps:golotto" => AppDetails(
                "golotto",
                "🎲 GoLotto"),

            "apps:combatfight" => AppDetails(
                "combatfight",
                "🥊 CombatFight"),

            "app:dobby:status" =>
                new BotResponse(
                    await _systemPowerService.GetRuntimeInfoAsync(cancellationToken),
                    DobbyMenus.DobbyAppMenu()),

            "app:dobby:logs" =>
                new BotResponse(
                    await _systemPowerService.GetDobbyLogsAsync(cancellationToken),
                    DobbyMenus.DobbyAppMenu(),
                    SendAsNewMessage: true),

            "app:dobby:healthcheck" =>
                new BotResponse(
                    """
🧪 This is Dobby Healthcheck

🟢 Bot process is responding.
🟢 Telegram callback routing is working.
""",
                    DobbyMenus.DobbyAppMenu()),

            _ when callbackData.EndsWith(":status", StringComparison.OrdinalIgnoreCase) &&
                   callbackData.StartsWith("app:", StringComparison.OrdinalIgnoreCase) =>
                AppOperationPlaceholder(callbackData, "📊 Status"),

            _ when callbackData.EndsWith(":logs", StringComparison.OrdinalIgnoreCase) &&
                   callbackData.StartsWith("app:", StringComparison.OrdinalIgnoreCase) =>
                AppOperationPlaceholder(
                    callbackData,
                    "📜 Logs",
                    sendAsNewMessage: true),

            _ when callbackData.EndsWith(":healthcheck", StringComparison.OrdinalIgnoreCase) &&
                   callbackData.StartsWith("app:", StringComparison.OrdinalIgnoreCase) =>
                AppOperationPlaceholder(callbackData, "🧪 Healthcheck"),

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
                    "Domain healthcheck üçün ayrıca domain registry əlavə edəcəyik.",
                    DobbyMenus.ProxyMenu()),

            "proxy:network" =>
                NotImplementedOperation(
                    "📡 Proxy Network",
                    "Docker proxy network inspector növbəti mərhələdə əlavə olunacaq.",
                    DobbyMenus.ProxyMenu()),

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
""",
                    confirmCallback: "downloader:cleanup-confirm",
                    cancelCallback: "menu:downloader"),

            "downloader:cleanup-confirm" =>
                NotImplementedOperation(
                    "🧹 Cleanup Temp",
                    "Downloader cleanup runner hələ qoşulmayıb.",
                    DobbyMenus.DownloaderMenu()),

            "downloader:cancel" => ClearAndReturnMainMenu(telegramUserId),

            "settings:reboot-request" =>
                Confirmation(
                    """
⚠️ Reboot server?

Bu əməliyyat serveri yenidən başladacaq.
""",
                    confirmCallback: "settings:reboot-confirm",
                    cancelCallback: "menu:settings"),

            "settings:reboot-confirm" =>
                new BotResponse(
                    await _systemPowerService.RebootAsync(cancellationToken),
                    DobbyMenus.SettingsMenu()),

            "settings:poweroff-request" =>
                Confirmation(
                    """
⚠️ Power off server?

Bu əməliyyat serveri söndürəcək.
""",
                    confirmCallback: "settings:poweroff-confirm",
                    cancelCallback: "menu:settings"),

            "settings:poweroff-confirm" =>
                new BotResponse(
                    await _systemPowerService.PowerOffAsync(cancellationToken),
                    DobbyMenus.SettingsMenu()),

            "settings:dobby-logs" =>
                new BotResponse(
                    await _systemPowerService.GetDobbyLogsAsync(cancellationToken),
                    DobbyMenus.SettingsMenu(),
                    SendAsNewMessage: true),

            "settings:runtime" =>
                new BotResponse(
                    await _systemPowerService.GetRuntimeInfoAsync(cancellationToken),
                    DobbyMenus.SettingsMenu()),

            _ => new BotResponse(
                """
Command tanınmadı.

Əsas menyuya qayıtdım.
""",
                DobbyMenus.MainMenu())
        };
    }

    private async Task<BotResponse> DockerGroups(
        CancellationToken cancellationToken)
    {
        var groups = await _dockerService.GetGroupsAsync(cancellationToken);

        var text = new StringBuilder();
        text.AppendLine("🐳 Docker");
        text.AppendLine();

        foreach (var group in groups)
        {
            text.AppendLine($"{group.Icon} {group.Title}: {group.Count}");
        }

        return new BotResponse(
            text.ToString().TrimEnd(),
            DobbyMenus.DockerGroupsMenu(groups));
    }

    private async Task<BotResponse> DockerGroup(
        string callbackData,
        CancellationToken cancellationToken)
    {
        var groupKey = callbackData.Replace(
            "docker:group:",
            "",
            StringComparison.OrdinalIgnoreCase);

        var group = await _dockerService.GetGroupAsync(
            groupKey,
            cancellationToken);

        if (group is null)
        {
            return new BotResponse(
                """
🐳 Docker

⚠️ Group tapılmadı.
""",
                DobbyMenus.MainMenu());
        }

        var text = new StringBuilder();
        text.AppendLine($"{group.Icon} {group.Title}");
        text.AppendLine();

        if (group.Containers.Count == 0)
        {
            text.AppendLine("Container yoxdur.");
        }
        else
        {
            foreach (var container in group.Containers.Take(20))
            {
                var icon = IsRunning(container)
                    ? "🟢"
                    : "🔴";

                text.AppendLine($"{icon} {container.Name}");
            }
        }

        return new BotResponse(
            text.ToString().TrimEnd(),
            DobbyMenus.DockerGroupMenu(group));
    }

    private async Task<BotResponse> DockerContainer(
        string callbackData,
        CancellationToken cancellationToken)
    {
        var parts = callbackData.Split(
            ':',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length < 3)
        {
            return await DockerGroups(cancellationToken);
        }

        var containerId = parts[2];

        if (parts.Length >= 4 &&
            parts[3].Equals("logs", StringComparison.OrdinalIgnoreCase))
        {
            var details = await _dockerService.GetContainerDetailsAsync(
                containerId,
                cancellationToken);

            var logs = await _dockerService.GetContainerLogsAsync(
                containerId,
                80,
                cancellationToken);

            return new BotResponse(
                $"""
📄 Logs: {details?.Container.Name ?? containerId}

{logs}
""",
                details is null
                    ? DobbyMenus.MainMenu()
                    : DobbyMenus.DockerContainerMenu(containerId, details.Container.GroupKey),
                SendAsNewMessage: true);
        }

        if (parts.Length >= 4 &&
            parts[3].Equals("inspect", StringComparison.OrdinalIgnoreCase))
        {
            var details = await _dockerService.GetContainerDetailsAsync(
                containerId,
                cancellationToken);

            var inspect = await _dockerService.GetContainerInspectSummaryAsync(
                containerId,
                cancellationToken);

            return new BotResponse(
                inspect,
                details is null
                    ? DobbyMenus.MainMenu()
                    : DobbyMenus.DockerContainerMenu(containerId, details.Container.GroupKey));
        }

        var groupKey = parts.Length >= 4
            ? DockerGroupKey.Normalize(parts[3])
            : DockerGroupKey.Other;

        var containerDetails = await _dockerService.GetContainerDetailsAsync(
            containerId,
            cancellationToken);

        if (containerDetails is null)
        {
            return new BotResponse(
                """
🐳 Docker

⚠️ Container tapılmadı.
""",
                DobbyMenus.MainMenu());
        }

        var container = containerDetails.Container;
        var stats = containerDetails.Stats;

        var statusIcon = IsRunning(container)
            ? "🟢"
            : "🔴";

        var text = $"""
🐳 {container.Name}

Status:
{statusIcon} {container.Status}

Image:
{container.Image}

Health:
{container.Health ?? "unknown"}

Uptime:
{container.Uptime}

CPU:
{stats?.Cpu ?? "unknown"}

RAM:
{stats?.Memory ?? "unknown"} ({stats?.MemoryPercent ?? "unknown"})

Network:
{stats?.Network ?? "unknown"}

Block IO:
{stats?.BlockIo ?? "unknown"}
""";

        return new BotResponse(
            text,
            DobbyMenus.DockerContainerMenu(container.Id, groupKey));
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
                    DobbyMenus.MainMenu()),

            "/logs" when parts.Length == 2 =>
                new BotResponse(
                    await _serviceControlService.LogsAsync(parts[1], cancellationToken),
                    DobbyMenus.MainMenu(),
                    SendAsNewMessage: true),

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

Nümunələr:
https://www.instagram.com/p/...
https://www.instagram.com/reel/...
https://t.me/channel/123
@username
username
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
Username tapıldı:
{request.Value}

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
            "🧦 Dobby",
            DobbyMenus.MainMenu());
    }

    private static BotResponse HomeLabMenu()
    {
        return new BotResponse(
            "🏠 HomeLab",
            DobbyMenus.HomeLabMenu());
    }

    private static BotResponse AppsMenu()
    {
        return new BotResponse(
            "🚀 Apps",
            DobbyMenus.AppsMenu());
    }

    private static BotResponse DataLayerMenu()
    {
        return new BotResponse(
            "🗄 Data Layer",
            DobbyMenus.DataLayerMenu());
    }

    private static BotResponse ProxyMenu()
    {
        return new BotResponse(
            "🌐 Proxy & SSL",
            DobbyMenus.ProxyMenu());
    }

    private static BotResponse BackupMenu()
    {
        return new BotResponse(
            "💾 Backup",
            DobbyMenus.BackupMenu());
    }

    private static BotResponse DownloaderMenu()
    {
        return new BotResponse(
            "📥 Downloader",
            DobbyMenus.DownloaderMenu());
    }

    private static BotResponse SettingsMenu()
    {
        return new BotResponse(
            "⚙️ Settings",
            DobbyMenus.SettingsMenu());
    }

    private static BotResponse DobbyAppDetails()
    {
        return new BotResponse(
            """
🧦 This is Dobby

📊 Status
📜 Logs
🧪 Healthcheck
🤖 Dobby AI
""",
            DobbyMenus.DobbyAppMenu());
    }

    private static BotResponse AppDetails(
        string appKey,
        string title)
    {
        return new BotResponse(
            $"""
{title}

App key:
{appKey}

Hazırda:
📊 Status
📜 Logs
🧪 Healthcheck
""",
            DobbyMenus.AppDetailsMenu(appKey));
    }

    private static BotResponse AppOperationPlaceholder(
        string callbackData,
        string operationTitle,
        bool sendAsNewMessage = false)
    {
        var appKey = ExtractAppKey(callbackData);

        return new BotResponse(
            $"""
{operationTitle}

App:
{appKey}

Real app operation runner hələ qoşulmayıb.
""",
            DobbyMenus.AppDetailsMenu(appKey),
            SendAsNewMessage: sendAsNewMessage);
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
""",
            (dynamic)replyMarkup);
    }

    private static BotResponse Help()
    {
        return new BotResponse(
            """
🧦 Dobby Help

/menu
/status
/docker
/memory
/disk
/load
/ai sual
/ask sual
/logs service-name
/restart service-name
""",
            DobbyMenus.MainMenu());
    }

    private static bool IsRunning(DockerContainerInfo container)
    {
        return container.State.Equals("running", StringComparison.OrdinalIgnoreCase) ||
               container.Status.StartsWith("Up", StringComparison.OrdinalIgnoreCase);
    }
}