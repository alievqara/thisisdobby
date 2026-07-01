namespace DobbyBot.Worker.Services.Docker;

public static class DockerGroupKey
{
    public const string HomeLab = "homelab";
    public const string Apps = "apps";
    public const string Ai = "ai";
    public const string Other = "other";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Other;
        }

        var normalized = value.Trim().ToLowerInvariant();

        return normalized switch
        {
            HomeLab => HomeLab,
            Apps => Apps,
            Ai => Ai,
            Other => Other,
            _ => Other
        };
    }
}