using DobbyBot.Worker.Bot;
using DobbyBot.Worker.Services.Docker;

namespace DobbyBot.Worker.Menus;

public static class DobbyMenus
{
    public static TelegramInlineKeyboardMarkup MainMenu()
    {
        return Keyboard(
            Row("🏠 HomeLab", "menu:homelab"),
            Row("🐳 Docker", "menu:docker"),
            Row("🚀 Apps", "menu:apps"),
            Row("🗄 Data Layer", "menu:data"),
            Row("🌐 Proxy & SSL", "menu:proxy"),
            Row("💾 Backup", "menu:backup"),
            Row("📥 Downloader", "menu:downloader"),
            Row("⚙️ Settings", "menu:settings"));
    }

    public static TelegramInlineKeyboardMarkup HomeLabMenu()
    {
        return Keyboard(
            Row("📊 Status", "homelab:status"),
            Row("💽 Disk", "homelab:disk"),
            Row("🧠 Memory", "homelab:memory"),
            Row("🔥 CPU / Load", "homelab:load"),
            Row("🧯 Failed Services", "homelab:failed-services"),
            Row("⬅️ Back", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup DockerGroupsMenu(
        IReadOnlyList<DockerContainerGroup> groups)
    {
        var rows = groups
            .Select(group =>
                Row(
                    $"{group.Icon} {group.Title} ({group.Count})",
                    $"docker:group:{group.Key}"))
            .ToList();

        rows.Add(Row("⬅️ Back", "menu:main"));

        return Keyboard(rows);
    }

    public static TelegramInlineKeyboardMarkup DockerGroupMenu(
        DockerContainerGroup group)
    {
        var rows = group.Containers
            .Take(20)
            .Select(container =>
            {
                var icon = IsRunning(container)
                    ? "🟢"
                    : "🔴";

                return Row(
                    $"{icon} {ShortName(container.Name)}",
                    $"docker:container:{container.Id}:{group.Key}");
            })
            .ToList();

        rows.Add(Row("⬅️ Docker", "menu:docker"));
        rows.Add(Row("🏠 Home", "menu:main"));

        return Keyboard(rows);
    }

    public static TelegramInlineKeyboardMarkup DockerContainerMenu(
        string containerId,
        string groupKey)
    {
        return Keyboard(
            Row(
                "📄 Logs",
                $"docker:container:{containerId}:logs"),
            Row(
                "📋 Inspect",
                $"docker:container:{containerId}:inspect"),
            Row(
                "⬅️ Group",
                $"docker:group:{groupKey}"),
            Row("🐳 Docker", "menu:docker"),
            Row("🏠 Home", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup AppsMenu()
    {
        return Keyboard(
            Row("🧦 This is Dobby", "apps:dobby"),
            Row("📅 Planzy", "apps:planzy"),
            Row("🥊 CombatFight", "apps:combatfight"),
            Row("⬅️ Back", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup DobbyAppMenu()
    {
        return Keyboard(
            Row("📊 Status", "app:dobby:status"),
            Row("📜 Logs", "app:dobby:logs"),
            Row("🧪 Healthcheck", "app:dobby:healthcheck"),
            Row("⬅️ Apps", "menu:apps"),
            Row("🏠 Home", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup AppDetailsMenu(
        string appKey)
    {
        return Keyboard(
            Row("📊 Status", $"app:{appKey}:status"),
            Row("📜 Logs", $"app:{appKey}:logs"),
            Row("🧪 Healthcheck", $"app:{appKey}:healthcheck"),
            Row("⬅️ Apps", "menu:apps"),
            Row("🏠 Home", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup DataLayerMenu()
    {
        return Keyboard(
            Row("🐘 PostgreSQL", "data:postgres"),
            Row("⚡ Redis", "data:redis"),
            Row("💾 Volumes", "data:volumes"),
            Row("🔐 DB Backups", "data:backups"),
            Row("⬅️ Back", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup ProxyMenu()
    {
        return Keyboard(
            Row("🌍 Proxy Status", "proxy:npm-status"),
            Row("🔐 SSL Certificates", "proxy:ssl"),
            Row("🧪 Check Domains", "proxy:domains"),
            Row("📡 Proxy Network", "proxy:network"),
            Row("⬅️ Back", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup BackupMenu()
    {
        return Keyboard(
            Row("🗂 Backup List", "backup:list"),
            Row("🧪 Verify Backup", "backup:verify"),
            Row("⬅️ Back", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup DownloaderMenu()
    {
        return Keyboard(
            Row(
                "📤 Send Telegram File",
                "downloader:telegram-file"),
            Row(
                "🔗 Send Link / Username",
                "downloader:input"),
            Row(
                "🗂 Downloads Folder",
                "downloader:folder"),
            Row(
                "🧹 Cleanup Temp",
                "downloader:cleanup-request"),
            Row("⬅️ Back", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup
        DownloaderPlatformSelectionMenu()
    {
        return Keyboard(
            Row(
                "Instagram",
                "downloader:platform:instagram"),
            Row(
                "Telegram",
                "downloader:platform:telegram"),
            Row(
                "Cancel",
                "downloader:cancel"));
    }

    public static TelegramInlineKeyboardMarkup SettingsMenu()
    {
        return Keyboard(
            Row("🔁 Reboot", "settings:reboot-request"),
            Row("⏻ Power Off", "settings:poweroff-request"),
            Row("📜 Dobby Logs", "settings:dobby-logs"),
            Row("🕒 Runtime / Boot Info", "settings:runtime"),
            Row("⬅️ Back", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup ConfirmationMenu(
        string confirmCallback,
        string cancelCallback = "menu:main")
    {
        return Keyboard(
            Row("✅ Confirm", confirmCallback),
            Row("❌ Cancel", cancelCallback));
    }

    private static TelegramInlineKeyboardMarkup Keyboard(
        params TelegramInlineKeyboardButton[][] rows)
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard = rows
        };
    }

    private static TelegramInlineKeyboardMarkup Keyboard(
        IReadOnlyCollection<TelegramInlineKeyboardButton[]> rows)
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard = rows.ToArray()
        };
    }

    private static TelegramInlineKeyboardButton[] Row(
        string text,
        string callbackData)
    {
        return
        [
            new TelegramInlineKeyboardButton
            {
                Text = text,
                CallbackData = callbackData
            }
        ];
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

    private static string ShortName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        return value.Length <= 32
            ? value
            : value[..29] + "...";
    }
}