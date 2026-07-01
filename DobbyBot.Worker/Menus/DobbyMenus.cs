using DobbyBot.Worker.Bot;
using DobbyBot.Worker.Services.Docker;

namespace DobbyBot.Worker.Menus;

public static class DobbyMenus
{
    public static TelegramInlineKeyboardMarkup MainMenu()
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("🏠 HomeLab", "menu:homelab"),
                    Button("🐳 Docker", "menu:docker")
                ],
                [
                    Button("🚀 Apps", "menu:apps"),
                    Button("🗄 Data Layer", "menu:data")
                ],
                [
                    Button("🌐 Proxy & SSL", "menu:proxy"),
                    Button("💾 Backup", "menu:backup")
                ],
                [
                    Button("📥 Downloader", "menu:downloader"),
                    Button("⚙️ Settings", "menu:settings")
                ]
            ]
        };
    }

    public static TelegramInlineKeyboardMarkup HomeLabMenu()
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("📊 Status", "homelab:status"),
                    Button("💽 Disk", "homelab:disk")
                ],
                [
                    Button("🧠 Memory", "homelab:memory"),
                    Button("🔥 CPU / Load", "homelab:load")
                ],
                [
                    Button("🧯 Failed Services", "homelab:failed-services")
                ],
                [
                    Button("⬅️ Back", "menu:main")
                ]
            ]
        };
    }

    public static TelegramInlineKeyboardMarkup DockerGroupsMenu(
    IReadOnlyList<DockerContainerGroup> groups)
    {
        var rows = new List<TelegramInlineKeyboardButton[]>();

        foreach (var group in groups)
        {
            rows.Add(
            [
                Button(
                $"{group.Icon} {group.Title} ({group.Count})",
                $"docker:group:{group.Key}")
            ]);
        }

        rows.Add(
        [
            Button("⬅️ Back", "menu:main")
        ]);

        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard = rows.ToArray()
        };
    }

    public static TelegramInlineKeyboardMarkup DockerGroupMenu(
    DockerContainerGroup group)
    {
        var rows = new List<TelegramInlineKeyboardButton[]>();

        foreach (var container in group.Containers.Take(20))
        {
            var icon = IsRunning(container)
                ? "🟢"
                : "🔴";

            rows.Add(
            [
                Button(
                $"{icon} {ShortName(container.Name)}",
                $"docker:container:{container.Id}:{group.Key}")
            ]);
        }

        rows.Add(
        [
            Button("⬅️ Docker", "menu:docker"),
        Button("🏠 Home", "menu:main")
        ]);

        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard = rows.ToArray()
        };
    }

    public static TelegramInlineKeyboardMarkup DockerContainerMenu(
        string containerId,
        string groupKey)
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("📄 Logs", $"docker:container:{containerId}:logs"),
                    Button("📋 Inspect", $"docker:container:{containerId}:inspect")
                ],
                [
                    Button("⬅️ Group", $"docker:group:{groupKey}"),
                    Button("🐳 Docker", "menu:docker")
                ],
                [
                    Button("🏠 Home", "menu:main")
                ]
            ]
        };
    }

    public static TelegramInlineKeyboardMarkup AppsMenu()
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("🧦 This is Dobby", "apps:dobby")
                ],
                [
                    Button("📅 Planzy", "apps:planzy"),
                ],
                [
                    Button("🎲 GoLotto", "apps:golotto")
                ],
                [
                    Button("🥊 CombatFight", "apps:combatfight")
                ],
                [
                    Button("⬅️ Back", "menu:main")
                ]
            ]
        };
    }

    public static TelegramInlineKeyboardMarkup DobbyAppMenu()
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("📊 Status", "app:dobby:status"),
                    Button("📜 Logs", "app:dobby:logs")
                ],
                [
                    Button("🧪 Healthcheck", "app:dobby:healthcheck"),
                    Button("🤖 Dobby AI", "apps:dobby-ai")
                ],
                [
                    Button("⬅️ Apps", "menu:apps"),
                    Button("🏠 Home", "menu:main")
                ]
            ]
        };
    }

    public static TelegramInlineKeyboardMarkup AppDetailsMenu(string appKey)
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("📊 Status", $"app:{appKey}:status"),
                    Button("📜 Logs", $"app:{appKey}:logs")
                ],
                [
                    Button("🧪 Healthcheck", $"app:{appKey}:healthcheck")
                ],
                [
                    Button("⬅️ Apps", "menu:apps"),
                    Button("🏠 Home", "menu:main")
                ]
            ]
        };
    }

    public static TelegramInlineKeyboardMarkup DataLayerMenu()
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("🐘 PostgreSQL", "data:postgres"),
                    Button("⚡ Redis", "data:redis")
                ],
                [
                    Button("💾 Volumes", "data:volumes"),
                    Button("🔐 DB Backups", "data:backups")
                ],
                [
                    Button("⬅️ Back", "menu:main")
                ]
            ]
        };
    }

    public static TelegramInlineKeyboardMarkup ProxyMenu()
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("🌍 Proxy Status", "proxy:npm-status")
                ],
                [
                    Button("🔐 SSL Certificates", "proxy:ssl"),
                    Button("🧪 Check Domains", "proxy:domains")
                ],
                [
                    Button("📡 Proxy Network", "proxy:network")
                ],
                [
                    Button("⬅️ Back", "menu:main")
                ]
            ]
        };
    }

    public static TelegramInlineKeyboardMarkup BackupMenu()
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("🗂 Backup List", "backup:list"),
                    Button("🧪 Verify Backup", "backup:verify")
                ],
                [
                    Button("⬅️ Back", "menu:main")
                ]
            ]
        };
    }

    public static TelegramInlineKeyboardMarkup DownloaderMenu()
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("📤 Send Telegram File", "downloader:telegram-file")
                ],
                [
                    Button("🔗 Send Link / Username", "downloader:input")
                ],
                [
                    Button("🗂 Downloads Folder", "downloader:folder"),
                    Button("🧹 Cleanup Temp", "downloader:cleanup-request")
                ],
                [
                    Button("⬅️ Back", "menu:main")
                ]
            ]
        };
    }

    public static TelegramInlineKeyboardMarkup DownloaderPlatformSelectionMenu()
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("Instagram", "downloader:platform:instagram"),
                    Button("Telegram", "downloader:platform:telegram")
                ],
                [
                    Button("Cancel", "downloader:cancel")
                ]
            ]
        };
    }

    public static TelegramInlineKeyboardMarkup SettingsMenu()
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("🔁 Reboot", "settings:reboot-request"),
                    Button("⏻ Power Off", "settings:poweroff-request")
                ],
                [
                    Button("📜 Dobby Logs", "settings:dobby-logs")
                ],
                [
                    Button("🕒 Runtime / Boot Info", "settings:runtime")
                ],
                [
                    Button("⬅️ Back", "menu:main")
                ]
            ]
        };
    }

    public static TelegramInlineKeyboardMarkup ConfirmationMenu(
        string confirmCallback,
        string cancelCallback = "menu:main")
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("✅ Confirm", confirmCallback),
                    Button("❌ Cancel", cancelCallback)
                ]
            ]
        };
    }

    private static TelegramInlineKeyboardButton Button(
        string text,
        string callbackData)
    {
        return new TelegramInlineKeyboardButton
        {
            Text = text,
            CallbackData = callbackData
        };
    }

    private static bool IsRunning(DockerContainerInfo container)
    {
        return container.State.Equals("running", StringComparison.OrdinalIgnoreCase) ||
               container.Status.StartsWith("Up", StringComparison.OrdinalIgnoreCase);
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