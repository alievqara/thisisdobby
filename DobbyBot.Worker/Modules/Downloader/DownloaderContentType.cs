namespace DobbyBot.Worker.Modules.Downloader;

public enum DownloaderContentType
{
    Unknown = 0,
    Username = 1,

    InstagramProfile = 10,
    InstagramPost = 11,
    InstagramReel = 12,
    InstagramStory = 13,

    TelegramChannelOrUser = 20,
    TelegramMessage = 21
}