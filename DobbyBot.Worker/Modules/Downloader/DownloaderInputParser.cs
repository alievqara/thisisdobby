namespace DobbyBot.Worker.Modules.Downloader;

public sealed class DownloaderInputParser : IDownloaderInputParser
{
    public DownloaderRequest Parse(string input)
    {
        var value = input.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            return Unknown(value);
        }

        if (TryParseSupportedUrl(value, out var uri))
        {
            return ParseUrl(uri, value);
        }

        if (IsUsername(value))
        {
            return new DownloaderRequest(
                Source: DownloaderSource.Unknown,
                ContentType: DownloaderContentType.Username,
                Value: NormalizeUsername(value),
                RequiresPlatformSelection: true);
        }

        return Unknown(value);
    }

    private static DownloaderRequest ParseUrl(Uri uri, string originalValue)
    {
        var host = uri.Host.ToLowerInvariant();
        var path = uri.AbsolutePath.Trim('/');

        if (host is "instagram.com" or "www.instagram.com")
        {
            return ParseInstagramUrl(path, originalValue);
        }

        if (host is "t.me" or "telegram.me")
        {
            return ParseTelegramUrl(path, originalValue);
        }

        return Unknown(originalValue);
    }

    private static DownloaderRequest ParseInstagramUrl(
        string path,
        string originalValue)
    {
        if (path.StartsWith("p/", StringComparison.OrdinalIgnoreCase))
        {
            return new DownloaderRequest(
                DownloaderSource.Instagram,
                DownloaderContentType.InstagramPost,
                originalValue,
                RequiresPlatformSelection: false);
        }

        if (path.StartsWith("reel/", StringComparison.OrdinalIgnoreCase))
        {
            return new DownloaderRequest(
                DownloaderSource.Instagram,
                DownloaderContentType.InstagramReel,
                originalValue,
                RequiresPlatformSelection: false);
        }

        if (path.StartsWith("stories/", StringComparison.OrdinalIgnoreCase))
        {
            return new DownloaderRequest(
                DownloaderSource.Instagram,
                DownloaderContentType.InstagramStory,
                originalValue,
                RequiresPlatformSelection: false);
        }

        var username = ExtractFirstPathSegment(path);

        if (!string.IsNullOrWhiteSpace(username))
        {
            return new DownloaderRequest(
                DownloaderSource.Instagram,
                DownloaderContentType.InstagramProfile,
                username,
                RequiresPlatformSelection: false);
        }

        return Unknown(originalValue);
    }

    private static DownloaderRequest ParseTelegramUrl(
        string path,
        string originalValue)
    {
        var parts = path.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length >= 2 && long.TryParse(parts[^1], out _))
        {
            return new DownloaderRequest(
                DownloaderSource.Telegram,
                DownloaderContentType.TelegramMessage,
                originalValue,
                RequiresPlatformSelection: false);
        }

        var username = ExtractFirstPathSegment(path);

        if (!string.IsNullOrWhiteSpace(username))
        {
            return new DownloaderRequest(
                DownloaderSource.Telegram,
                DownloaderContentType.TelegramChannelOrUser,
                username,
                RequiresPlatformSelection: false);
        }

        return Unknown(originalValue);
    }

    private static bool TryParseSupportedUrl(string input, out Uri uri)
    {
        var value = input.Trim();

        var looksLikeSupportedUrl =
            value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("instagram.com/", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("www.instagram.com/", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("t.me/", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("telegram.me/", StringComparison.OrdinalIgnoreCase);

        if (!looksLikeSupportedUrl)
        {
            uri = null!;
            return false;
        }

        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "https://" + value;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out uri!) &&
               !string.IsNullOrWhiteSpace(uri.Host);
    }

    private static bool IsUsername(string input)
    {
        var value = NormalizeUsername(input);

        if (value.Length is < 3 or > 32)
        {
            return false;
        }

        return value.All(c =>
            char.IsLetterOrDigit(c) ||
            c == '_' ||
            c == '.');
    }

    private static string NormalizeUsername(string input)
    {
        return input.Trim().TrimStart('@');
    }

    private static string ExtractFirstPathSegment(string path)
    {
        return path.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
    }

    private static DownloaderRequest Unknown(string value)
    {
        return new DownloaderRequest(
            DownloaderSource.Unknown,
            DownloaderContentType.Unknown,
            value,
            RequiresPlatformSelection: false);
    }
}