using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DobbyBot.Worker.Services.Apps;

public sealed class AppManagementOptions
{
    public List<ManagedRepository> Repositories { get; init; } = [];
}

public sealed class ManagedRepository
{
    public string Name { get; init; } = "";
    public string Directory { get; init; } = "";
    public string Branch { get; init; } = "main";
    public string ComposeFile { get; init; } = "compose.yaml";
    public string ProjectName { get; init; } = "";
}

public sealed record HomelabApp(string Id, string Name, string Project, string Status, ManagedRepository? Repository);

public sealed class AppManagementService(IOptions<AppManagementOptions> options, IAppProcessRunner runner)
{
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    public static string Id(string name) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..16].ToLowerInvariant();

    public async Task<IReadOnlyList<HomelabApp>> DiscoverAsync(CancellationToken ct)
    {
        var result = await runner.RunAsync("docker", ["compose", "ls", "--all", "--format", "json"], ct);
        using var json = JsonDocument.Parse(result);
        var apps = json.RootElement.EnumerateArray().Select(item =>
        {
            var name = item.GetProperty("Name").GetString()!;
            var repo = options.Value.Repositories.FirstOrDefault(r => Project(r) == name);
            return new HomelabApp(Id(name), repo?.Name ?? name, name, item.GetProperty("Status").GetString() ?? "unknown", repo);
        }).ToList();
        foreach (var repo in options.Value.Repositories)
        {
            var name = Project(repo);
            if (apps.All(a => a.Project != name)) apps.Add(new(Id(name), repo.Name, name, "Quraşdırılmayıb / dayanıb", repo));
        }
        return apps.OrderBy(a => a.Name).ToList();
    }

    public async Task<string> ReadAsync(HomelabApp app, string action, CancellationToken ct)
    {
        if (action == "git")
        {
            var repo = Validate(app);
            var head = await runner.RunAsync("git", ["-C", repo.Directory, "log", "-1", "--format=%h • %s (%ci)"], ct);
            var status = await runner.RunAsync("git", ["-C", repo.Directory, "status", "--short"], ct);
            return $"Branch: {repo.Branch}\nSon commit: {head}\n\n{(status.Length == 0 ? "İş qovluğu təmizdir." : "Yerli dəyişikliklər var; yeniləmə bloklanacaq.\n" + status)}";
        }
        return await runner.RunAsync("docker", ["compose", "-p", app.Project, "ps", "--all"], ct, action == "logs"
            ? ["compose", "-p", app.Project, "logs", "--no-color", "--tail", "35"] : null);
    }

    public async Task<string> OperateAsync(HomelabApp app, string action, CancellationToken ct)
    {
        if (!await _operationLock.WaitAsync(0, ct)) return "⏳ Başqa əməliyyat davam edir. Bir az sonra yenidən yoxla.";
        try
        {
            var repo = Validate(app);
            var prefix = new[] { "compose", "--project-directory", repo.Directory, "-f", Path.Combine(repo.Directory, repo.ComposeFile), "-p", app.Project };
            if (action == "update")
            {
                var status = await runner.RunAsync("git", ["-C", repo.Directory, "status", "--porcelain"], ct);
                if (status.Length > 0) return "⛔ Yerli dəyişikliklər var. Yeniləmədən əvvəl onları commit et.";
                var branch = await runner.RunAsync("git", ["-C", repo.Directory, "branch", "--show-current"], ct);
                if (branch != repo.Branch) return $"⛔ Aktiv branch {repo.Branch} olmalıdır.";
                await runner.RunAsync("git", ["-C", repo.Directory, "pull", "--ff-only", "origin", repo.Branch], ct);
                await runner.RunAsync("docker", [..prefix, "build", "--pull"], ct);
                await runner.RunAsync("docker", [..prefix, "pull", "--ignore-buildable"], ct);
                await runner.RunAsync("docker", [..prefix, "up", "-d", "--build", "--wait", "--wait-timeout", "120"], ct);
                return "✅ Git yeniləndi, image-lər quruldu və tətbiq başladıldı.\n\n" + await ReadAsync(app, "status", ct);
            }
            if (action is not ("restart" or "start" or "stop")) throw new InvalidOperationException("Əməliyyat tanınmadı.");
            return await runner.RunAsync("docker", [..prefix, ..(action == "start" ? new[] { "up", "-d", "--wait" } : new[] { action })], ct);
        }
        finally { _operationLock.Release(); }
    }

    private static string Project(ManagedRepository repo) => string.IsNullOrWhiteSpace(repo.ProjectName) ? repo.Name.ToLowerInvariant() : repo.ProjectName;
    private static ManagedRepository Validate(HomelabApp app)
    {
        var repo = app.Repository ?? throw new InvalidOperationException("İdarə etmək üçün bu tətbiqi Homelab:Repositories siyahısına əlavə et.");
        if (!Path.IsPathFullyQualified(repo.Directory) || !System.IO.Directory.Exists(repo.Directory)) throw new InvalidOperationException("Repo üçün mövcud tam qovluq yolu lazımdır.");
        var root = Path.GetFullPath(repo.Directory) + Path.DirectorySeparatorChar;
        var file = Path.GetFullPath(Path.Combine(repo.Directory, repo.ComposeFile));
        if (!file.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) || !File.Exists(file)) throw new InvalidOperationException("Compose faylı repo daxilində olmalıdır və mövcud deyil.");
        return repo;
    }

}


