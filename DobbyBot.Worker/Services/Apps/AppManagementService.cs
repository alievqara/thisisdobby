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
        if (action == "check")
        {
            var repo = Validate(app);
            await runner.RunAsync("git", ["-C", repo.Directory, "fetch", "origin", repo.Branch], ct);
            var delta = await runner.RunAsync("git", ["-C", repo.Directory, "rev-list", "--left-right", "--count", "HEAD...FETCH_HEAD"], ct);
            var counts = delta.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (counts.Length != 2 || !int.TryParse(counts[0], out var ahead) || !int.TryParse(counts[1], out var behind))
                throw new InvalidOperationException("Git versiya fərqi oxunmadı.");
            var commits = behind == 0 ? "Yeni commit yoxdur." : await runner.RunAsync("git", ["-C", repo.Directory, "log", "--oneline", "-8", "HEAD..FETCH_HEAD"], ct);
            return $"🔎 origin/{repo.Branch}\n\n📥 {behind} yeni commit\n📤 {ahead} yerli commit\n\n{commits}\n\nKod və konteynerlər dəyişdirilmədi.";
        }
        // Labels work for discovered Compose projects without a local compose file.
        if (action == "status")
            return await runner.RunAsync("docker", ["ps", "--all", "--filter", $"label=com.docker.compose.project={app.Project}", "--format", "{{.Names}} | {{.State}} | {{.Status}} | {{.Ports}}"], ct);
        if (action == "logs")
        {
            var output = await runner.RunAsync("docker", ["ps", "--all", "--filter", $"label=com.docker.compose.project={app.Project}", "--format", "{{.ID}}"], ct);
            var ids = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (ids.Length == 0) return "Konteyner tapılmadı.";
            var logs = new List<string>();
            foreach (var id in ids.Take(6))
            {
                var text = await runner.RunAsync("docker", ["logs", "--timestamps", "--tail", "15", id], ct);
                logs.Add($"📦 {id}\n{PanelText.Clip(text, 420)}");
            }
            return string.Join("\n\n", logs) + (ids.Length > 6 ? "\n\nİlk 6 konteyner göstərilir." : "");
        }
        throw new InvalidOperationException("Əməliyyat tanınmadı.");
    }
    public async Task<string> OperateAsync(HomelabApp app, string action, CancellationToken ct, Action<string>? progress = null)
    {
        if (!await _operationLock.WaitAsync(0, ct)) throw new InvalidOperationException("Başqa əməliyyat davam edir.");
        try
        {
            var repo = Validate(app);
            var prefix = new[] { "compose", "--project-directory", repo.Directory, "-f", Path.Combine(repo.Directory, repo.ComposeFile), "-p", app.Project };
            if (action == "update")
            {
                progress?.Invoke("🔎 Repo və branch yoxlanır");
                var status = await runner.RunAsync("git", ["-C", repo.Directory, "status", "--porcelain"], ct);
                if (status.Length > 0) throw new InvalidOperationException("⛔ Yerli dəyişikliklər var. Yeniləmədən əvvəl onları commit et.");
                var branch = await runner.RunAsync("git", ["-C", repo.Directory, "branch", "--show-current"], ct);
                if (branch != repo.Branch) throw new InvalidOperationException($"⛔ Aktiv branch {repo.Branch} olmalıdır.");
                progress?.Invoke("📥 GitHub-dan kod çəkilir");
                await runner.RunAsync("git", ["-C", repo.Directory, "pull", "--ff-only", "origin", repo.Branch], ct);
                progress?.Invoke("🔨 Image-lər qurulur");
                await runner.RunAsync("docker", [..prefix, "build", "--pull"], ct);
                progress?.Invoke("📦 Image-lər yüklənir");
                await runner.RunAsync("docker", [..prefix, "pull", "--ignore-buildable"], ct);
                progress?.Invoke("🩺 Tətbiq başladılır və sağlamlığı yoxlanır");
                await runner.RunAsync("docker", [..prefix, "up", "-d", "--no-build", "--wait", "--wait-timeout", "120"], ct);
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
