namespace DobbyBot.Worker.Modules.Downloader;

public interface IDownloaderInputParser
{
    DownloaderRequest Parse(string input);
}