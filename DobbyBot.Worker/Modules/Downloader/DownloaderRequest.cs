namespace DobbyBot.Worker.Modules.Downloader;

public sealed record DownloaderRequest(
    DownloaderSource Source,
    DownloaderContentType ContentType,
    string Value,
    bool RequiresPlatformSelection);