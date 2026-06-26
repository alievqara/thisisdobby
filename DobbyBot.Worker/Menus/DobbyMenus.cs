using DobbyBot.Worker.Bot;

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
                    Button("🖥 Server", "menu:server"),
                    Button("🧩 Containers", "menu:containers")
                ],
                [
                    Button("🚀 Apps", "menu:apps"),
                    Button("🗄 Data Layer", "menu:data")
                ],
                [
                    Button("🌐 Proxy & SSL", "menu:proxy"),
                    Button("📊 Monitoring", "menu:monitoring")
                ],
                [
                    Button("💾 Backup", "menu:backup"),
                    Button("📥 Downloader", "menu:downloader")
                ],
                [
                    Button("🤖 AI Assistant", "menu:ai"),
                    Button("⚙️ Settings", "menu:settings")
                ],
                [
                    Button("❓ Help", "help")
                ]
            ]
        };
    }

    public static TelegramInlineKeyboardMarkup ServerMenu()
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("📊 Status", "server:status"),
                    Button("💽 Disk", "server:disk")
                ],
                [
                    Button("🧠 Memory", "server:memory"),
                    Button("🔥 CPU / Load", "server:load")
                ],
                [
                    Button("🧯 Failed Services", "server:failed-services")
                ],
                [
                    Button("🔁 Reboot Request", "server:reboot-request")
                ],
                [
                    Button("⬅️ Back", "menu:main")
                ]
            ]
        };
    }

    public static TelegramInlineKeyboardMarkup ContainersMenu()
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("📦 All Containers", "containers:all"),
                    Button("✅ Running", "containers:running")
                ],
                [
                    Button("❌ Failed / Exited", "containers:failed")
                ],
                [
                    Button("📜 Logs", "containers:logs"),
                    Button("🔄 Restart", "containers:restart")
                ],
                [
                    Button("⬅️ Back", "menu:main")
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
                    Button("🎲 GoLotto", "apps:golotto")
                ],
                [
                    Button("🥊 CombatFight", "apps:combatfight")
                ],
                [
                    Button("➕ Add App", "apps:add")
                ],
                [
                    Button("⬅️ Back", "menu:main")
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
                    Button("🔄 Restart", $"app:{appKey}:restart-request"),
                    Button("⬆️ Update", $"app:{appKey}:update-request")
                ],
                [
                    Button("💾 Backup DB", $"app:{appKey}:backup-db-request"),
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
                    Button("🌍 NPM Status", "proxy:npm-status")
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

    public static TelegramInlineKeyboardMarkup MonitoringMenu()
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("💚 Uptime Kuma", "monitoring:kuma")
                ],
                [
                    Button("📈 Health Summary", "monitoring:summary"),
                    Button("🚨 Alerts", "monitoring:alerts")
                ],
                [
                    Button("🧾 Recent Incidents", "monitoring:incidents")
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
                    Button("📦 Backup Planzy DB", "backup:planzy-db-request")
                ],
                [
                    Button("📦 Backup GoLotto DB", "backup:golotto-db-request")
                ],
                [
                    Button("📦 Backup Dobby Config", "backup:dobby-config-request")
                ],
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

    public static TelegramInlineKeyboardMarkup AiMenu()
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("🧠 Ask Dobby", "ai:ask")
                ],
                [
                    Button("📋 Explain Logs", "ai:explain-logs"),
                    Button("🧪 Diagnose Error", "ai:diagnose")
                ],
                [
                    Button("🛠 Suggest Fix", "ai:suggest-fix")
                ],
                [
                    Button("⬅️ Back", "menu:main")
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
                    Button("👤 Admin", "settings:admin"),
                    Button("🔐 Security", "settings:security")
                ],
                [
                    Button("🧾 Audit Logs", "settings:audit"),
                    Button("🧠 State", "settings:state")
                ],
                [
                    Button("ℹ️ About Dobby", "settings:about")
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
}