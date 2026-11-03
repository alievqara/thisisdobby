using DobbyBot.Worker.Bot;
using DobbyBot.Worker.Services.Docker;

namespace DobbyBot.Worker.Menus;

public static class DobbyMenus
{
    public static TelegramInlineKeyboardMarkup MainMenu()
    {
        return Keyboard(
            [new() { Text = "◎ Sistem", CallbackData = "menu:homelab" }, new() { Text = "▦ Tətbiqlər", CallbackData = "menu:apps" }],
            [new() { Text = "◫ Konteynerlər", CallbackData = "menu:docker" }, new() { Text = "▤ Verilənlər", CallbackData = "menu:data" }],
            [new() { Text = "↗ Şəbəkə / SSL", CallbackData = "menu:proxy" }, new() { Text = "◷ Ehtiyat nüsxələr", CallbackData = "menu:backup" }],
            [new() { Text = "↓ Yükləmələr", CallbackData = "menu:downloader" }, new() { Text = "⚙ İdarəetmə", CallbackData = "menu:settings" }]);
    }

    public static TelegramInlineKeyboardMarkup HomeLabMenu()
    {
        return Keyboard(
            Row("◎ Ümumi vəziyyət", "homelab:status"),
            Row("◴ Disk", "homelab:disk"),
            Row("▥ Yaddaş", "homelab:memory"),
            Row("▥ CPU / Yük", "homelab:load"),
            Row("⚠ Xətalı servislər", "homelab:failed-services"),
            Row("‹ Geri", "menu:main"));
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

        rows.Add(Row("‹ Geri", "menu:main"));

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

        rows.Add(Row("‹ Konteynerlər", "menu:docker"));
        rows.Add(Row("⌂ Əsas panel", "menu:main"));

        return Keyboard(rows);
    }

    public static TelegramInlineKeyboardMarkup DockerContainerMenu(
        string containerId,
        string groupKey)
    {
        return Keyboard(
            Row(
                "≡ Loqlar",
                $"docker:container:{containerId}:logs"),
            Row(
                "◎ Detallar",
                $"docker:container:{containerId}:inspect"),
            Row(
                "‹ Qrup",
                $"docker:group:{groupKey}"),
            Row("◫ Konteynerlər", "menu:docker"),
            Row("⌂ Əsas panel", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup DobbyAppMenu()
    {
        return Keyboard(
            Row("◎ Ümumi vəziyyət", "app:dobby:status"),
            Row("📜 Logs", "app:dobby:logs"),
            Row("🧪 Healthcheck", "app:dobby:healthcheck"),
            Row("⬅️ Apps", "menu:apps"),
            Row("⌂ Əsas panel", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup DataLayerMenu()
    {
        return Keyboard(
            Row("🐘 PostgreSQL", "data:postgres"),
            Row("⚡ Redis", "data:redis"),
            Row("▤ Disk həcmləri", "data:volumes"),
            Row("◷ DB nüsxələri", "data:backups"),
            Row("‹ Geri", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup ProxyMenu()
    {
        return Keyboard(
            Row("◎ Proxy vəziyyəti", "proxy:npm-status"),
            Row("◇ SSL sertifikatları", "proxy:ssl"),
            Row("◎ Domain yoxlaması", "proxy:domains"),
            Row("↗ Proxy şəbəkəsi", "proxy:network"),
            Row("‹ Geri", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup BackupMenu()
    {
        return Keyboard(
            Row("≡ Nüsxələrin siyahısı", "backup:list"),
            Row("◎ Nüsxəni yoxla", "backup:verify"),
            Row("‹ Geri", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup DownloaderMenu()
    {
        return Keyboard(
            Row(
                "↑ Fayl göndər",
                "downloader:telegram-file"),
            Row(
                "↗ Link / İstifadəçi adı",
                "downloader:input"),
            Row(
                "▤ Yükləmə qovluğu",
                "downloader:folder"),
            Row(
                "× Keşi təmizlə",
                "downloader:cleanup-request"),
            Row("‹ Geri", "menu:main"));
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
            Row("↻ Serveri yenidən başlat", "settings:reboot-request"),
            Row("⏻ Serveri söndür", "settings:poweroff-request"),
            Row("≡ Bot loqları", "settings:dobby-logs"),
            Row("◷ İşləmə müddəti", "settings:runtime"),
            Row("‹ Geri", "menu:main"));
    }

    public static TelegramInlineKeyboardMarkup ConfirmationMenu(
        string confirmCallback,
        string cancelCallback = "menu:main")
    {
        return Keyboard(
            Row("✓ Təsdiqlə", confirmCallback),
            Row("× Ləğv et", cancelCallback));
    }

    private static TelegramInlineKeyboardMarkup Keyboard(
        params TelegramInlineKeyboardButton[][] rows)
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard = Compact(rows)
        };
    }

    private static TelegramInlineKeyboardMarkup Keyboard(
        IReadOnlyCollection<TelegramInlineKeyboardButton[]> rows)
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard = Compact(rows.ToArray())
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

    private static TelegramInlineKeyboardButton[][] Compact(TelegramInlineKeyboardButton[][] rows)
    {
        var result = new List<TelegramInlineKeyboardButton[]>();
        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (row.Length == 1 && i + 1 < rows.Length && rows[i + 1].Length == 1 &&
                !row[0].Text.StartsWith("‹") && !rows[i + 1][0].Text.StartsWith("‹") &&
                row[0].Text.Length + rows[i + 1][0].Text.Length < 43)
                result.Add([row[0], rows[++i][0]]);
            else result.Add(row);
        }
        return result.ToArray();
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
