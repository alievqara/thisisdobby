using System.Text;
using DobbyBot.Worker.Services.Apps;
using DobbyBot.Worker.Bot;
using DobbyBot.Worker.Menus;
using DobbyBot.Worker.Modules.Downloader;
using DobbyBot.Worker.Services;
using DobbyBot.Worker.Services.Docker;
using DobbyBot.Worker.Services.SystemPower;
using DobbyBot.Worker.State;

namespace DobbyBot.Worker.Commands;

public sealed class CommandRouter
{
    private const string HiddenDockerGroupKey = "ai";

    private readonly AppsPanel _appsPanel;
    private readonly IServerStatusService _serverStatusService;
    private readonly IDownloaderInputParser _downloaderInputParser;
    private readonly IUserStateService _userStateService;
    private readonly IDockerService _dockerService;
    private readonly ISystemPowerService _systemPowerService;

    public CommandRouter(
        IServerStatusService serverStatusService,
        IDownloaderInputParser downloaderInputParser,
        IUserStateService userStateService,
        IDockerService dockerService,
        ISystemPowerService systemPowerService, AppsPanel appsPanel)
    {
        _serverStatusService = serverStatusService;
        _downloaderInputParser = downloaderInputParser;
        _userStateService = userStateService;
        _dockerService = dockerService;
        _systemPowerService = systemPowerService;
        _appsPanel = appsPanel;
    }

    public bool ExpectsTextInput(long telegramUserId)
    {
        var state = _userStateService.Get(telegramUserId);

        return state.Mode ==
               BotUserMode.WaitingForDownloaderInput;
    }

    public Task<BotResponse> HandleAsync(
        long telegramUserId,
        string text,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedText = text.Trim();

        if (normalizedText.Equals(
                "/start",
                StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Equals(
                "/menu",
                StringComparison.OrdinalIgnoreCase))
        {
            _userStateService.Clear(telegramUserId);

            return Task.FromResult(MainMenu());
        }

        if (ExpectsTextInput(telegramUserId))
        {
            return Task.FromResult(
                HandleDownloaderInput(
                    telegramUserId,
                    normalizedText));
        }

        return Task.FromResult(
            new BotResponse(
                "🧦 Dobby yalnız menu ilə idarə olunur. /menu yaz."));
    }

    public async Task<BotResponse> HandleCallbackAsync(
        long telegramUserId,
        string callbackData,
        CancellationToken cancellationToken)
    {
        if ((callbackData.StartsWith("apps:", StringComparison.Ordinal) && callbackData != "apps:dobby") ||
            (callbackData.StartsWith("app:", StringComparison.Ordinal) && !callbackData.StartsWith("app:dobby:", StringComparison.Ordinal)))
            return await _appsPanel.HandleAsync(telegramUserId, "menu:apps", cancellationToken);

        if (callbackData == "menu:apps" || callbackData.StartsWith("managed:", StringComparison.Ordinal))
            return await _appsPanel.HandleAsync(telegramUserId, callbackData, cancellationToken);

        return callbackData switch
        {
            "menu:main" =>
                ClearAndReturnMainMenu(telegramUserId),

            "menu:homelab" =>
                HomeLabMenu(),

            "menu:docker" =>
                await DockerGroups(cancellationToken),

            "menu:apps" =>
                await _appsPanel.HandleAsync(telegramUserId, "menu:apps", cancellationToken),

            "menu:data" =>
                DataLayerMenu(),

            "menu:proxy" =>
                ProxyMenu(),

            "menu:backup" =>
                BackupMenu(),

            "menu:downloader" =>
                DownloaderMenu(),

            "menu:settings" =>
                SettingsMenu(),

            "homelab:status" =>
                new BotResponse(
                    await _serverStatusService.GetStatusAsync(
                        cancellationToken),
                    DobbyMenus.HomeLabMenu()),

            "homelab:disk" =>
                new BotResponse(
                    await _serverStatusService.GetDiskStatusAsync(
                        cancellationToken),
                    DobbyMenus.HomeLabMenu()),

            "homelab:memory" =>
                new BotResponse(
                    await _serverStatusService.GetMemoryStatusAsync(
                        cancellationToken),
                    DobbyMenus.HomeLabMenu()),

            "homelab:load" =>
                new BotResponse(
                    await _serverStatusService.GetLoadStatusAsync(
                        cancellationToken),
                    DobbyMenus.HomeLabMenu()),

            "homelab:failed-services" =>
                new BotResponse(
                    await _serverStatusService.GetFailedServicesAsync(
                        cancellationToken),
                    DobbyMenus.HomeLabMenu()),

            _ when callbackData.StartsWith(
                "docker:group:",
                StringComparison.OrdinalIgnoreCase) =>
                await DockerGroup(
                    callbackData,
                    cancellationToken),

            _ when callbackData.StartsWith(
                "docker:container:",
                StringComparison.OrdinalIgnoreCase) =>
                await DockerContainer(
                    callbackData,
                    cancellationToken),

            "apps:dobby" =>
                DobbyAppDetails(),

            "app:dobby:status" =>
                new BotResponse(
                    await _systemPowerService.GetRuntimeInfoAsync(
                        cancellationToken),
                    DobbyMenus.DobbyAppMenu()),

            "app:dobby:logs" =>
                new BotResponse(
                    await _systemPowerService.GetDobbyLogsAsync(
                        cancellationToken),
                    DobbyMenus.DobbyAppMenu()),

            "app:dobby:healthcheck" =>
                new BotResponse(
                    """
                    🧪 This is Dobby Healthcheck

                    🟢 Bot process is responding.
                    🟢 Telegram callback routing is working.
                    """,
                    DobbyMenus.DobbyAppMenu()),

            "data:postgres" =>
                new BotResponse(
                    await _serverStatusService.GetPostgresStatusAsync(
                        cancellationToken),
                    DobbyMenus.DataLayerMenu()),

            "data:redis" =>
                new BotResponse(
                    await _serverStatusService.GetRedisStatusAsync(
                        cancellationToken),
                    DobbyMenus.DataLayerMenu()),

            "data:volumes" =>
                new BotResponse(
                    await _serverStatusService.GetVolumesStatusAsync(
                        cancellationToken),
                    DobbyMenus.DataLayerMenu()),

            "data:backups" =>
                new BotResponse(
                    await _serverStatusService.GetDbBackupsStatusAsync(
                        cancellationToken),
                    DobbyMenus.DataLayerMenu()),

            "proxy:npm-status" =>
                new BotResponse(
                    await _serverStatusService.GetProxyStatusAsync(
                        cancellationToken),
                    DobbyMenus.ProxyMenu()),

            "proxy:ssl" =>
                new BotResponse(
                    await _serverStatusService.GetSslStatusAsync(
                        cancellationToken),
                    DobbyMenus.ProxyMenu()),

            "proxy:domains" =>
                NotImplementedOperation(
                    "🧪 Check Domains",
                    "Domain healthcheck üçün ayrıca domain registry əlavə ediləcək.",
                    DobbyMenus.ProxyMenu()),

            "proxy:network" =>
                NotImplementedOperation(
                    "📡 Proxy Network",
                    "Docker proxy network inspector növbəti mərhələdə əlavə ediləcək.",
                    DobbyMenus.ProxyMenu()),

            "backup:list" =>
                new BotResponse(
                    await _serverStatusService.GetDbBackupsStatusAsync(
                        cancellationToken),
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

                    Fayl qəbul etmə və storage yazma axını hələ qoşulmayıb.
                    """,
                    DobbyMenus.DownloaderMenu()),

            "downloader:input" =>
                StartDownloaderInputMode(telegramUserId),

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
                    confirmCallback:
                        "downloader:cleanup-confirm",
                    cancelCallback:
                        "menu:downloader"),

            "downloader:cleanup-confirm" =>
                NotImplementedOperation(
                    "🧹 Cleanup Temp",
                    "Downloader cleanup runner hələ qoşulmayıb.",
                    DobbyMenus.DownloaderMenu()),

            "downloader:cancel" =>
                ClearAndReturnMainMenu(telegramUserId),

            "settings:reboot-request" =>
                Confirmation(
                    """
                    ⚠️ Reboot server?

                    Bu əməliyyat serveri yenidən başladacaq.
                    """,
                    confirmCallback:
                        "settings:reboot-confirm",
                    cancelCallback:
                        "menu:settings"),

            "settings:reboot-confirm" =>
                new BotResponse(
                    await _systemPowerService.RebootAsync(
                        cancellationToken),
                    DobbyMenus.SettingsMenu()),

            "settings:poweroff-request" =>
                Confirmation(
                    """
                    ⚠️ Power off server?

                    Bu əməliyyat serveri söndürəcək.
                    """,
                    confirmCallback:
                        "settings:poweroff-confirm",
                    cancelCallback:
                        "menu:settings"),

            "settings:poweroff-confirm" =>
                new BotResponse(
                    await _systemPowerService.PowerOffAsync(
                        cancellationToken),
                    DobbyMenus.SettingsMenu()),

            "settings:dobby-logs" =>
                new BotResponse(
                    await _systemPowerService.GetDobbyLogsAsync(
                        cancellationToken),
                    DobbyMenus.SettingsMenu()),

            "settings:runtime" =>
                new BotResponse(
                    await _systemPowerService.GetRuntimeInfoAsync(
                        cancellationToken),
                    DobbyMenus.SettingsMenu()),

            _ =>
                new BotResponse(
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
        var allGroups =
            await _dockerService.GetGroupsAsync(
                cancellationToken);

        var visibleGroups = allGroups
            .Where(group =>
                !group.Key.Equals(
                    HiddenDockerGroupKey,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        var text = new StringBuilder();

        text.AppendLine("🐳 Docker");
        text.AppendLine();

        foreach (var group in visibleGroups)
        {
            text.AppendLine(
                $"{group.Icon} {group.Title}: {group.Count}");
        }

        return new BotResponse(
            text.ToString().TrimEnd(),
            DobbyMenus.DockerGroupsMenu(visibleGroups));
    }

    private async Task<BotResponse> DockerGroup(
        string callbackData,
        CancellationToken cancellationToken)
    {
        var groupKey = callbackData.Replace(
            "docker:group:",
            string.Empty,
            StringComparison.OrdinalIgnoreCase);

        if (groupKey.Equals(
                HiddenDockerGroupKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return await DockerGroups(cancellationToken);
        }

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

        text.AppendLine(
            $"{group.Icon} {group.Title}");
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

                text.AppendLine(
                    $"{icon} {container.Name}");
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
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        if (parts.Length < 3)
        {
            return await DockerGroups(cancellationToken);
        }

        var containerId = parts[2];

        if (parts.Length >= 4 &&
            parts[3].Equals(
                "logs",
                StringComparison.OrdinalIgnoreCase))
        {
            var details =
                await _dockerService.GetContainerDetailsAsync(
                    containerId,
                    cancellationToken);

            var logs =
                await _dockerService.GetContainerLogsAsync(
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
                    : DobbyMenus.DockerContainerMenu(
                        containerId,
                        NormalizeVisibleGroupKey(
                            details.Container.GroupKey)));
        }

        if (parts.Length >= 4 &&
            parts[3].Equals(
                "inspect",
                StringComparison.OrdinalIgnoreCase))
        {
            var details =
                await _dockerService.GetContainerDetailsAsync(
                    containerId,
                    cancellationToken);

            var inspect =
                await _dockerService.GetContainerInspectSummaryAsync(
                    containerId,
                    cancellationToken);

            return new BotResponse(
                inspect,
                details is null
                    ? DobbyMenus.MainMenu()
                    : DobbyMenus.DockerContainerMenu(
                        containerId,
                        NormalizeVisibleGroupKey(
                            details.Container.GroupKey)));
        }

        var requestedGroupKey = parts.Length >= 4
            ? parts[3]
            : DockerGroupKey.Other;

        var groupKey = NormalizeVisibleGroupKey(
            DockerGroupKey.Normalize(requestedGroupKey));

        var containerDetails =
            await _dockerService.GetContainerDetailsAsync(
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
            DobbyMenus.DockerContainerMenu(
                container.Id,
                groupKey));
    }

    private BotResponse StartDownloaderInputMode(
        long telegramUserId)
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

        if (request.ContentType ==
            DownloaderContentType.Unknown)
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

        if (string.IsNullOrWhiteSpace(
                state.PendingDownloaderInput))
        {
            _userStateService.Clear(telegramUserId);

            return new BotResponse(
                "Pending username tapılmadı. Yenidən göndər.",
                DobbyMenus.DownloaderMenu());
        }

        var contentType = source switch
        {
            DownloaderSource.Instagram =>
                DownloaderContentType.InstagramProfile,

            DownloaderSource.Telegram =>
                DownloaderContentType.TelegramChannelOrUser,

            _ =>
                DownloaderContentType.Unknown
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

    private BotResponse ClearAndReturnMainMenu(
        long telegramUserId)
    {
        _userStateService.Clear(telegramUserId);

        return MainMenu();
    }

    private static BotResponse MainMenu()
    {
        return new BotResponse(
            "DOBBY · HOMELAB CONTROL\n━━━━━━━━━━━━━━━━━━━━\n\nServer, tətbiqlər və infrastruktur bir paneldə.\nİdarə etmək istədiyin bölməni seç.",
            DobbyMenus.MainMenu());
    }

    private static BotResponse HomeLabMenu()
    {
        return new BotResponse(
            "SİSTEM MONİTORU\n━━━━━━━━━━━━━━━━━━━━\n\nResurslar və servis sağlamlığı",
            DobbyMenus.HomeLabMenu());
    }

    private static BotResponse DataLayerMenu()
    {
        return new BotResponse(
            "VERİLƏNLƏR\n━━━━━━━━━━━━━━━━━━━━\n\nBazalar, disk həcmləri və ehtiyat nüsxələr",
            DobbyMenus.DataLayerMenu());
    }

    private static BotResponse ProxyMenu()
    {
        return new BotResponse(
            "ŞƏBƏKƏ VƏ SSL\n━━━━━━━━━━━━━━━━━━━━\n\nProxy vəziyyəti və sertifikatlar",
            DobbyMenus.ProxyMenu());
    }

    private static BotResponse BackupMenu()
    {
        return new BotResponse(
            "EHTİYAT NÜSXƏLƏR\n━━━━━━━━━━━━━━━━━━━━\n\nSaxlanmış nüsxələri yoxla",
            DobbyMenus.BackupMenu());
    }

    private static BotResponse DownloaderMenu()
    {
        return new BotResponse(
            "YÜKLƏMƏLƏR\n━━━━━━━━━━━━━━━━━━━━\n\nFayl və link alətləri",
            DobbyMenus.DownloaderMenu());
    }

    private static BotResponse SettingsMenu()
    {
        return new BotResponse(
            "İDARƏETMƏ\n━━━━━━━━━━━━━━━━━━━━\n\nBot diaqnostikası və server əməliyyatları",
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
            """,
            DobbyMenus.DobbyAppMenu());
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
        TelegramInlineKeyboardMarkup replyMarkup)
    {
        return new BotResponse(
            $"""
             {title}

             {body}
             """,
            replyMarkup);
    }

    private static string NormalizeVisibleGroupKey(
        string groupKey)
    {
        return groupKey.Equals(
            HiddenDockerGroupKey,
            StringComparison.OrdinalIgnoreCase)
            ? DockerGroupKey.Other
            : groupKey;
    }

    private static bool IsRunning(
        DockerContainerInfo container)
    {
        return container.State.Equals(
                   "running",
                   StringComparison.OrdinalIgnoreCase) ||
               container.Status.StartsWith(
                   "Up",
                   StringComparison.OrdinalIgnoreCase);
    }
}
