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
                Button("🤖 AI Assistant", "menu:ai")
            ],
            [
                Button("🧪 HomeLab", "menu:homelab"),
                Button("💾 Backup", "menu:backup")
            ],
            [
                Button("📥 Downloader", "menu:downloader"),
                Button("⚙️ Settings", "menu:settings")
            ],
            [
                Button("❓ Help", "help")
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
                Button("📥 Send Link / Username", "downloader:input")
            ],
            [
                Button("⬅️ Main Menu", "menu:main")
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

    public static TelegramInlineKeyboardMarkup ServerMenu()
    {
        return new TelegramInlineKeyboardMarkup
        {
            InlineKeyboard =
            [
                [
                    Button("📊 Status", "server:status"),
                    Button("🧩 Services", "server:services")
                ],
                [
                    Button("📜 GoLotto Logs", "server:logs:golotto")
                ],
                [
                    Button("🔄 Restart GoLotto", "server:restart:golotto")
                ],
                [
                    Button("⬅️ Main Menu", "menu:main")
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
                    Button("💬 Ask Dobby", "ai:ask")
                ],
                [
                    Button("🧠 Local AI Status", "ai:local-status")
                ],
                [
                    Button("⬅️ Main Menu", "menu:main")
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
                    Button("🐳 Docker", "homelab:docker")
                ],
                [
                    Button("🌐 Nginx", "homelab:nginx"),
                    Button("📡 Network", "homelab:network")
                ],
                [
                    Button("⬅️ Main Menu", "menu:main")
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
                    Button("💾 Backup Now", "backup:now")
                ],
                [
                    Button("📦 Last Backups", "backup:last")
                ],
                [
                    Button("⬅️ Main Menu", "menu:main")
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
                    Button("ℹ️ Bot Info", "settings:bot-info")
                ],
                [
                    Button("👤 Admin Info", "settings:admin-info")
                ],
                [
                    Button("⬅️ Main Menu", "menu:main")
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