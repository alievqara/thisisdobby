using System.Diagnostics;
using System.Text;

namespace DobbyBot.Worker.Services.Docker;

public sealed class DockerService : IDockerService
{
    private const string Separator = "\u001f";

    public async Task<IReadOnlyList<DockerContainerGroup>> GetGroupsAsync(
        CancellationToken cancellationToken = default)
    {
        var containers = await GetContainersAsync(cancellationToken);

        return BuildGroups(containers);
    }

    public async Task<DockerContainerGroup?> GetGroupAsync(
        string groupKey,
        CancellationToken cancellationToken = default)
    {
        var normalized = DockerGroupKey.Normalize(groupKey);
        var groups = await GetGroupsAsync(cancellationToken);

        return groups.FirstOrDefault(x =>
            x.Key.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<DockerContainerDetails?> GetContainerDetailsAsync(
        string containerId,
        CancellationToken cancellationToken = default)
    {
        var containers = await GetContainersAsync(cancellationToken);

        var container = containers.FirstOrDefault(x =>
            x.Id.Equals(containerId, StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals(containerId, StringComparison.OrdinalIgnoreCase));

        if (container is null)
        {
            return null;
        }

        var stats = await GetStatsAsync(container.Id, cancellationToken);
        var health = await GetHealthAsync(container.Id, cancellationToken);
        var restartPolicy = await InspectValueAsync(
            container.Id,
            "{{.HostConfig.RestartPolicy.Name}}",
            cancellationToken);

        var networkSummary = await InspectValueAsync(
            container.Id,
            "{{range $k,$v := .NetworkSettings.Networks}}{{$k}} {{end}}",
            cancellationToken);

        var mountsSummary = await InspectValueAsync(
            container.Id,
            "{{len .Mounts}} mount(s)",
            cancellationToken);

        var createdAt = await InspectValueAsync(
            container.Id,
            "{{.Created}}",
            cancellationToken);

        var enrichedContainer = container with
        {
            Health = string.IsNullOrWhiteSpace(health) ? container.Health : health
        };

        return new DockerContainerDetails(
            enrichedContainer,
            stats,
            Clean(restartPolicy),
            Clean(networkSummary),
            Clean(mountsSummary),
            Clean(createdAt));
    }

    public async Task<string> GetContainerLogsAsync(
        string containerId,
        int tail = 80,
        CancellationToken cancellationToken = default)
    {
        var safeTail = Math.Clamp(tail, 20, 200);

        var output = await RunAsync(
            "docker",
            ["logs", "--tail", safeTail.ToString(), containerId],
            cancellationToken);

        if (string.IsNullOrWhiteSpace(output))
        {
            return "Log tapılmadı və ya container mövcud deyil.";
        }

        var lines = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .TakeLast(safeTail)
            .Select(x => Truncate(x, 160));

        return string.Join('\n', lines);
    }

    public async Task<string> GetContainerInspectSummaryAsync(
        string containerId,
        CancellationToken cancellationToken = default)
    {
        var details = await GetContainerDetailsAsync(containerId, cancellationToken);

        if (details is null)
        {
            return """
📋 Inspect

⚠️ Container tapılmadı.
""";
        }

        var container = details.Container;

        return $"""
📋 Inspect Summary

🐳 Container:
{container.Name}

Image:
{container.Image}

State:
{container.State}

Status:
{container.Status}

Health:
{container.Health ?? "unknown"}

Restart Policy:
{details.RestartPolicy}

Networks:
{details.NetworkSummary}

Mounts:
{details.MountsSummary}

Created:
{details.CreatedAt}
""";
    }

    private static async Task<IReadOnlyList<DockerContainerInfo>> GetContainersAsync(
        CancellationToken cancellationToken)
    {
        var output = await RunAsync(
            "docker",
            [
                "ps",
                "-a",
                "--format",
                $"{{{{.ID}}}}{Separator}{{{{.Names}}}}{Separator}{{{{.Image}}}}{Separator}{{{{.Status}}}}{Separator}{{{{.State}}}}"
            ],
            cancellationToken);

        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var containers = new List<DockerContainerInfo>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(Separator);

            if (parts.Length < 5)
            {
                continue;
            }

            var id = Clean(parts[0]);
            var name = Clean(parts[1]);
            var image = Clean(parts[2]);
            var status = Clean(parts[3]);
            var state = Clean(parts[4]);
            var uptime = ExtractUptime(status);
            var groupKey = DetectGroup(name, image);

            containers.Add(new DockerContainerInfo(
                id,
                name,
                image,
                status,
                state,
                Health: null,
                uptime,
                groupKey));
        }

        return containers
            .OrderBy(x => GroupOrder(x.GroupKey))
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static IReadOnlyList<DockerContainerGroup> BuildGroups(
        IReadOnlyList<DockerContainerInfo> containers)
    {
        var groups = new[]
        {
            CreateGroup(
                DockerGroupKey.HomeLab,
                "📦",
                "HomeLab",
                containers),

            CreateGroup(
                DockerGroupKey.Apps,
                "🚀",
                "Apps",
                containers),

            CreateGroup(
                DockerGroupKey.Ai,
                "🤖",
                "AI",
                containers),

            CreateGroup(
                DockerGroupKey.Other,
                "🧪",
                "Other",
                containers)
        };

        return groups;
    }

    private static DockerContainerGroup CreateGroup(
        string key,
        string icon,
        string title,
        IReadOnlyList<DockerContainerInfo> containers)
    {
        return new DockerContainerGroup(
            key,
            icon,
            title,
            containers
                .Where(x => x.GroupKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(IsRunning)
                .ThenBy(x => x.Name)
                .ToList());
    }

    private static string DetectGroup(
        string name,
        string image)
    {
        var value = $"{name} {image}".ToLowerInvariant();

        if (value.Contains("dobby-postgres") ||
            value.Contains("dobby-redis") ||
            value.Contains("dobby-portainer") ||
            value.Contains("dobby-npm") ||
            value.Contains("dobby-uptime-kuma") ||
            value.Contains("nginx-proxy-manager") ||
            value.Contains("portainer") ||
            value.Contains("uptime-kuma"))
        {
            return DockerGroupKey.HomeLab;
        }

        if (value.Contains("ollama") ||
            value.Contains("open-webui") ||
            value.Contains("dobby-ai") ||
            value.Contains("llm") ||
            value.Contains("llama") ||
            value.Contains("qwen") ||
            value.Contains("claude"))
        {
            return DockerGroupKey.Ai;
        }

        if (value.Contains("planzy") ||
            value.Contains("golotto") ||
            value.Contains("combatfight") ||
            value.Contains("thisisdobby") ||
            value.Contains("dobbybot"))
        {
            return DockerGroupKey.Apps;
        }

        return DockerGroupKey.Other;
    }

    private static async Task<DockerContainerStats?> GetStatsAsync(
        string containerId,
        CancellationToken cancellationToken)
    {
        var output = await RunAsync(
            "docker",
            [
                "stats",
                "--no-stream",
                "--format",
                $"{{{{.CPUPerc}}}}{Separator}{{{{.MemUsage}}}}{Separator}{{{{.MemPerc}}}}{Separator}{{{{.NetIO}}}}{Separator}{{{{.BlockIO}}}}",
                containerId
            ],
            cancellationToken);

        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var parts = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault()?
            .Split(Separator);

        if (parts is null || parts.Length < 5)
        {
            return null;
        }

        return new DockerContainerStats(
            Clean(parts[0]),
            Clean(parts[1]),
            Clean(parts[2]),
            Clean(parts[3]),
            Clean(parts[4]));
    }

    private static async Task<string> GetHealthAsync(
        string containerId,
        CancellationToken cancellationToken)
    {
        var output = await InspectValueAsync(
            containerId,
            "{{if .State.Health}}{{.State.Health.Status}}{{else}}unknown{{end}}",
            cancellationToken);

        return Clean(output);
    }

    private static async Task<string> InspectValueAsync(
        string containerId,
        string template,
        CancellationToken cancellationToken)
    {
        return await RunAsync(
            "docker",
            ["inspect", "--format", template, containerId],
            cancellationToken);
    }

    private static async Task<string> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process();

            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            return string.IsNullOrWhiteSpace(output)
                ? error
                : output;
        }
        catch
        {
            return "";
        }
    }

    private static bool IsRunning(DockerContainerInfo container)
    {
        return container.State.Equals("running", StringComparison.OrdinalIgnoreCase) ||
               container.Status.StartsWith("Up", StringComparison.OrdinalIgnoreCase);
    }

    private static int GroupOrder(string groupKey)
    {
        return groupKey switch
        {
            DockerGroupKey.HomeLab => 0,
            DockerGroupKey.Apps => 1,
            DockerGroupKey.Ai => 2,
            DockerGroupKey.Other => 3,
            _ => 99
        };
    }

    private static string ExtractUptime(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "unknown";
        }

        if (!status.StartsWith("Up ", StringComparison.OrdinalIgnoreCase))
        {
            return "-";
        }

        var withoutUp = status[3..];

        var beforeParen = withoutUp.Split('(').FirstOrDefault();

        return string.IsNullOrWhiteSpace(beforeParen)
            ? withoutUp.Trim()
            : beforeParen.Trim();
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : value.Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength] + "...";
    }
}