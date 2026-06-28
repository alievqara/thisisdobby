using System.Diagnostics;
using System.Text;

namespace DobbyBot.Worker.Services;

public sealed class ServerStatusService : IServerStatusService
{
    private const string HomeLabPath = "/srv/homelab";
    private const string BackupPath = "/srv/homelab/backups";

    public async Task<string> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var host = Clean(await RunAsync("hostname", [], cancellationToken));
        var os = ParseOs(await RunAsync("cat", ["/etc/os-release"], cancellationToken));
        var uptime = Clean(await RunAsync("uptime", ["-p"], cancellationToken)).Replace("up ", "");
        var load = ParseLoad(await RunAsync("cat", ["/proc/loadavg"], cancellationToken));
        var memory = ParseMemory(await RunAsync("free", ["-h"], cancellationToken));
        var disk = ParseDisk(await RunAsync("df", ["-h", "/"], cancellationToken));
        var ip = ParseIp(await RunAsync("hostname", ["-I"], cancellationToken));

        var failedServices = Clean(await BashAsync("systemctl --failed --no-legend 2>/dev/null | wc -l", cancellationToken));
        var dockerRunning = Clean(await BashAsync("docker ps -q 2>/dev/null | wc -l", cancellationToken));
        var dockerTotal = Clean(await BashAsync("docker ps -aq 2>/dev/null | wc -l", cancellationToken));
        var dockerExited = Clean(await BashAsync("docker ps -aq --filter status=exited 2>/dev/null | wc -l", cancellationToken));

        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        return $"""
🧙‍♂️ Dobby Server Status

🟢 Server: online
🖥 Host: {host}
🐧 OS: {os}
⏱ Uptime: {uptime}

⚙️ System
• CPU Load: {load}
• RAM: {memory}
• Disk: {disk}
• IP: {ip}
• Failed Services: {failedServices}

🐳 Docker
• Running: {dockerRunning}
• Exited: {dockerExited}
• Total: {dockerTotal}

🕒 Updated: {now}
""";
    }

    public async Task<string> GetMemoryStatusAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunAsync("free", ["-h"], cancellationToken);

        var mem = ParseMemoryDetailed(output, "Mem:");
        var swap = ParseMemoryDetailed(output, "Swap:");

        var topContainers = await BashAsync(
            "docker stats --no-stream --format '{{.Name}}|{{.MemUsage}}|{{.MemPerc}}' 2>/dev/null | head -10",
            cancellationToken);

        var sb = new StringBuilder();

        sb.AppendLine("🧠 Memory");
        sb.AppendLine();

        if (mem is null)
        {
            sb.AppendLine("⚠️ RAM məlumatı oxunmadı.");
        }
        else
        {
            sb.AppendLine("🟢 RAM");
            sb.AppendLine($"• Used: {mem.Used}");
            sb.AppendLine($"• Total: {mem.Total}");
            sb.AppendLine($"• Free: {mem.Free}");
            sb.AppendLine($"• Available: {mem.Available}");
        }

        sb.AppendLine();

        if (swap is null)
        {
            sb.AppendLine("⚪ Swap: unknown");
        }
        else
        {
            sb.AppendLine("💿 Swap");
            sb.AppendLine($"• Used: {swap.Used}");
            sb.AppendLine($"• Total: {swap.Total}");
            sb.AppendLine($"• Free: {swap.Free}");
        }

        sb.AppendLine();

        sb.AppendLine("🐳 Top Memory Containers");

        if (string.IsNullOrWhiteSpace(topContainers))
        {
            sb.AppendLine("• Container memory stats yoxdur və ya Docker icazəsi yoxdur.");
        }
        else
        {
            foreach (var line in SplitLines(topContainers))
            {
                var parts = line.Split('|');
                var name = parts.ElementAtOrDefault(0) ?? "unknown";
                var usage = parts.ElementAtOrDefault(1) ?? "unknown";
                var percent = parts.ElementAtOrDefault(2) ?? "unknown";

                sb.AppendLine($"• {name}: {usage} ({percent})");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<string> GetDiskStatusAsync(CancellationToken cancellationToken = default)
    {
        var rootDisk = await RunAsync("df", ["-h", "/"], cancellationToken);
        var homelabDisk = await BashAsync($"df -h {HomeLabPath} 2>/dev/null | tail -n +2", cancellationToken);
        var dockerDf = await BashAsync("docker system df 2>/dev/null", cancellationToken);
        var backupSize = await BashAsync($"du -sh {BackupPath} 2>/dev/null | awk '{{print $1}}'", cancellationToken);

        var sb = new StringBuilder();

        sb.AppendLine("💽 Disk");
        sb.AppendLine();

        sb.AppendLine("🧱 Root");
        sb.AppendLine($"• Usage: {ParseDisk(rootDisk)}");
        sb.AppendLine();

        sb.AppendLine("🏠 HomeLab");
        if (string.IsNullOrWhiteSpace(homelabDisk))
        {
            sb.AppendLine($"• Path: {HomeLabPath}");
            sb.AppendLine("• Status: folder tapılmadı və ya icazə yoxdur");
        }
        else
        {
            sb.AppendLine($"• Usage: {ParseDfLine(homelabDisk)}");
        }

        sb.AppendLine();

        sb.AppendLine("💾 Backups");
        sb.AppendLine($"• Path: {BackupPath}");
        sb.AppendLine($"• Size: {(string.IsNullOrWhiteSpace(backupSize) ? "unknown" : Clean(backupSize))}");
        sb.AppendLine();

        sb.AppendLine("🐳 Docker Disk");
        if (string.IsNullOrWhiteSpace(dockerDf))
        {
            sb.AppendLine("• Docker disk məlumatı yoxdur və ya icazə yoxdur.");
        }
        else
        {
            foreach (var line in SplitLines(dockerDf).Take(8))
            {
                sb.AppendLine($"• {line}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<string> GetLoadStatusAsync(CancellationToken cancellationToken = default)
    {
        var load = ParseLoad(await RunAsync("cat", ["/proc/loadavg"], cancellationToken));
        var uptimeRaw = Clean(await RunAsync("uptime", [], cancellationToken));
        var cores = Clean(await RunAsync("nproc", [], cancellationToken));

        var topProcesses = await BashAsync(
            "ps -eo pid,comm,%cpu,%mem --sort=-%cpu | head -8",
            cancellationToken);

        var topContainers = await BashAsync(
            "docker stats --no-stream --format '{{.Name}}|{{.CPUPerc}}|{{.MemPerc}}' 2>/dev/null | head -10",
            cancellationToken);

        var sb = new StringBuilder();

        sb.AppendLine("🔥 CPU / Load");
        sb.AppendLine();

        sb.AppendLine("⚙️ System");
        sb.AppendLine($"• CPU Cores: {cores}");
        sb.AppendLine($"• Load Avg: {load}");
        sb.AppendLine($"• Uptime Raw: {uptimeRaw}");
        sb.AppendLine();

        sb.AppendLine("🧩 Top CPU Processes");
        if (string.IsNullOrWhiteSpace(topProcesses))
        {
            sb.AppendLine("• Process məlumatı oxunmadı.");
        }
        else
        {
            foreach (var line in SplitLines(topProcesses).Skip(1).Take(7))
            {
                sb.AppendLine($"• {line}");
            }
        }

        sb.AppendLine();

        sb.AppendLine("🐳 Top CPU Containers");
        if (string.IsNullOrWhiteSpace(topContainers))
        {
            sb.AppendLine("• Docker stats yoxdur və ya icazə yoxdur.");
        }
        else
        {
            foreach (var line in SplitLines(topContainers))
            {
                var parts = line.Split('|');
                var name = parts.ElementAtOrDefault(0) ?? "unknown";
                var cpu = parts.ElementAtOrDefault(1) ?? "unknown";
                var mem = parts.ElementAtOrDefault(2) ?? "unknown";

                sb.AppendLine($"• {name}: CPU {cpu}, RAM {mem}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<string> GetFailedServicesAsync(CancellationToken cancellationToken = default)
    {
        var output = await BashAsync(
            "systemctl --failed --no-legend 2>/dev/null",
            cancellationToken);

        if (string.IsNullOrWhiteSpace(output))
        {
            return """
🧯 Failed Services

🟢 Failed systemd service yoxdur.

Status:
• systemctl --failed temizdir
""";
        }

        var sb = new StringBuilder();

        sb.AppendLine("🧯 Failed Services");
        sb.AppendLine();

        foreach (var line in SplitLines(output).Take(15))
        {
            sb.AppendLine($"🔴 {line}");
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<string> GetAllContainersAsync(CancellationToken cancellationToken = default)
    {
        var output = await DockerContainersAsync("docker ps -a", cancellationToken);

        return FormatContainers(
            title: "🐳 All Containers",
            output: output,
            emptyMessage: "Container tapılmadı və ya Docker icazəsi yoxdur.");
    }

    public async Task<string> GetRunningContainersAsync(CancellationToken cancellationToken = default)
    {
        var output = await DockerContainersAsync("docker ps", cancellationToken);

        return FormatContainers(
            title: "✅ Running Containers",
            output: output,
            emptyMessage: "Hazırda running container yoxdur və ya Docker icazəsi yoxdur.");
    }

    public async Task<string> GetFailedContainersAsync(CancellationToken cancellationToken = default)
    {
        var output = await DockerContainersAsync("docker ps -a --filter status=exited --filter status=dead", cancellationToken);

        if (string.IsNullOrWhiteSpace(output))
        {
            return """
❌ Failed / Exited Containers

🟢 Failed və ya exited container tapılmadı.
""";
        }

        return FormatContainers(
            title: "❌ Failed / Exited Containers",
            output: output,
            emptyMessage: "Failed container tapılmadı.");
    }

    public async Task<string> GetContainerLogsSummaryAsync(CancellationToken cancellationToken = default)
    {
        var containers = await BashAsync(
            "docker ps --format '{{.Names}}' 2>/dev/null | head -5",
            cancellationToken);

        if (string.IsNullOrWhiteSpace(containers))
        {
            return """
📜 Container Logs

⚠️ Running container tapılmadı və ya Docker icazəsi yoxdur.
""";
        }

        var sb = new StringBuilder();

        sb.AppendLine("📜 Container Logs Summary");
        sb.AppendLine("Son 5 container üçün son 5 log sətri:");
        sb.AppendLine();

        foreach (var container in SplitLines(containers))
        {
            var logs = await BashAsync(
                $"docker logs --tail 5 {EscapeShellArg(container)} 2>&1",
                cancellationToken);

            sb.AppendLine($"🧩 {container}");

            if (string.IsNullOrWhiteSpace(logs))
            {
                sb.AppendLine("• Log yoxdur.");
            }
            else
            {
                foreach (var line in SplitLines(logs).Take(5))
                {
                    sb.AppendLine($"• {Truncate(line, 120)}");
                }
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<string> GetPostgresStatusAsync(CancellationToken cancellationToken = default)
    {
        var containers = await BashAsync(
            "docker ps -a --format '{{.Names}}|{{.Status}}|{{.Image}}' 2>/dev/null | grep -Ei 'postgres|pgsql|postgis' || true",
            cancellationToken);

        var firstName = SplitLines(containers)
            .Select(x => x.Split('|').FirstOrDefault())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        var ping = string.IsNullOrWhiteSpace(firstName)
            ? ""
            : await BashAsync($"docker exec {EscapeShellArg(firstName)} pg_isready 2>/dev/null || true", cancellationToken);

        var sb = new StringBuilder();

        sb.AppendLine("🐘 PostgreSQL");
        sb.AppendLine();

        if (string.IsNullOrWhiteSpace(containers))
        {
            sb.AppendLine("⚠️ PostgreSQL container tapılmadı.");
            sb.AppendLine();
            sb.AppendLine("Axtarılan adlar:");
            sb.AppendLine("• postgres");
            sb.AppendLine("• pgsql");
            sb.AppendLine("• postgis");
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine("📦 Containers");
        foreach (var line in SplitLines(containers).Take(10))
        {
            var parts = line.Split('|');
            var name = parts.ElementAtOrDefault(0) ?? "unknown";
            var status = parts.ElementAtOrDefault(1) ?? "unknown";
            var image = parts.ElementAtOrDefault(2) ?? "unknown";

            var icon = IsRunning(status) ? "🟢" : "🔴";

            sb.AppendLine($"{icon} {name}");
            sb.AppendLine($"• Status: {status}");
            sb.AppendLine($"• Image: {image}");
            sb.AppendLine();
        }

        sb.AppendLine("🧪 Health");
        sb.AppendLine(string.IsNullOrWhiteSpace(ping)
            ? "• pg_isready: unknown"
            : $"• pg_isready: {Clean(ping)}");

        return sb.ToString().TrimEnd();
    }

    public async Task<string> GetRedisStatusAsync(CancellationToken cancellationToken = default)
    {
        var containers = await BashAsync(
            "docker ps -a --format '{{.Names}}|{{.Status}}|{{.Image}}' 2>/dev/null | grep -Ei 'redis|valkey' || true",
            cancellationToken);

        var firstName = SplitLines(containers)
            .Select(x => x.Split('|').FirstOrDefault())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        var ping = string.IsNullOrWhiteSpace(firstName)
            ? ""
            : await BashAsync($"docker exec {EscapeShellArg(firstName)} redis-cli ping 2>/dev/null || true", cancellationToken);

        var info = string.IsNullOrWhiteSpace(firstName)
            ? ""
            : await BashAsync($"docker exec {EscapeShellArg(firstName)} redis-cli info memory 2>/dev/null | grep -E 'used_memory_human|maxmemory_human' || true", cancellationToken);

        var sb = new StringBuilder();

        sb.AppendLine("⚡ Redis");
        sb.AppendLine();

        if (string.IsNullOrWhiteSpace(containers))
        {
            sb.AppendLine("⚠️ Redis container tapılmadı.");
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine("📦 Containers");
        foreach (var line in SplitLines(containers).Take(10))
        {
            var parts = line.Split('|');
            var name = parts.ElementAtOrDefault(0) ?? "unknown";
            var status = parts.ElementAtOrDefault(1) ?? "unknown";
            var image = parts.ElementAtOrDefault(2) ?? "unknown";

            var icon = IsRunning(status) ? "🟢" : "🔴";

            sb.AppendLine($"{icon} {name}");
            sb.AppendLine($"• Status: {status}");
            sb.AppendLine($"• Image: {image}");
            sb.AppendLine();
        }

        sb.AppendLine("🧪 Health");
        sb.AppendLine($"• Ping: {(string.IsNullOrWhiteSpace(ping) ? "unknown" : Clean(ping))}");

        if (!string.IsNullOrWhiteSpace(info))
        {
            sb.AppendLine();
            sb.AppendLine("🧠 Memory");
            foreach (var line in SplitLines(info))
            {
                sb.AppendLine($"• {line}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<string> GetVolumesStatusAsync(CancellationToken cancellationToken = default)
    {
        var count = Clean(await BashAsync("docker volume ls -q 2>/dev/null | wc -l", cancellationToken));
        var volumes = await BashAsync("docker volume ls --format '{{.Name}}' 2>/dev/null | head -20", cancellationToken);
        var dockerDf = await BashAsync("docker system df -v 2>/dev/null | head -30", cancellationToken);

        var sb = new StringBuilder();

        sb.AppendLine("💾 Docker Volumes");
        sb.AppendLine();

        sb.AppendLine($"📦 Total Volumes: {count}");
        sb.AppendLine();

        sb.AppendLine("🗂 Volume List");
        if (string.IsNullOrWhiteSpace(volumes))
        {
            sb.AppendLine("• Volume tapılmadı və ya Docker icazəsi yoxdur.");
        }
        else
        {
            foreach (var line in SplitLines(volumes))
            {
                sb.AppendLine($"• {line}");
            }
        }

        sb.AppendLine();

        sb.AppendLine("📊 Docker Storage Summary");
        if (string.IsNullOrWhiteSpace(dockerDf))
        {
            sb.AppendLine("• Docker storage məlumatı yoxdur.");
        }
        else
        {
            foreach (var line in SplitLines(dockerDf).Take(15))
            {
                sb.AppendLine($"• {line}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<string> GetDbBackupsStatusAsync(CancellationToken cancellationToken = default)
    {
        var exists = await BashAsync($"test -d {EscapeShellArg(BackupPath)} && echo yes || echo no", cancellationToken);
        var size = await BashAsync($"du -sh {EscapeShellArg(BackupPath)} 2>/dev/null | awk '{{print $1}}'", cancellationToken);
        var files = await BashAsync(
            $"find {EscapeShellArg(BackupPath)} -maxdepth 3 -type f 2>/dev/null | sort | tail -10",
            cancellationToken);

        var sb = new StringBuilder();

        sb.AppendLine("🔐 DB Backups");
        sb.AppendLine();

        sb.AppendLine($"📁 Path: {BackupPath}");
        sb.AppendLine($"• Exists: {(Clean(exists) == "yes" ? "yes" : "no")}");
        sb.AppendLine($"• Size: {(string.IsNullOrWhiteSpace(size) ? "unknown" : Clean(size))}");
        sb.AppendLine();

        sb.AppendLine("🗂 Latest Backup Files");
        if (string.IsNullOrWhiteSpace(files))
        {
            sb.AppendLine("• Backup file tapılmadı.");
        }
        else
        {
            foreach (var file in SplitLines(files))
            {
                var details = await BashAsync($"ls -lh {EscapeShellArg(file)} 2>/dev/null | awk '{{print $5 \" | \" $6 \" \" $7 \" \" $8 \" | \" $9}}'", cancellationToken);
                sb.AppendLine($"• {Clean(details)}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<string> GetProxyStatusAsync(CancellationToken cancellationToken = default)
    {
        var containers = await BashAsync(
            "docker ps -a --format '{{.Names}}|{{.Status}}|{{.Image}}|{{.Ports}}' 2>/dev/null | grep -Ei 'nginx|npm|proxy|caddy|traefik' || true",
            cancellationToken);

        var networks = await BashAsync(
            "docker network ls --format '{{.Name}}|{{.Driver}}|{{.Scope}}' 2>/dev/null | grep -Ei 'proxy|nginx|npm|dobby' || true",
            cancellationToken);

        var sb = new StringBuilder();

        sb.AppendLine("🌐 Proxy & SSL");
        sb.AppendLine();

        sb.AppendLine("🌍 Proxy Containers");
        if (string.IsNullOrWhiteSpace(containers))
        {
            sb.AppendLine("⚠️ Proxy container tapılmadı.");
            sb.AppendLine("Axtarılan adlar: nginx, npm, proxy, caddy, traefik");
        }
        else
        {
            foreach (var line in SplitLines(containers).Take(10))
            {
                var parts = line.Split('|');
                var name = parts.ElementAtOrDefault(0) ?? "unknown";
                var status = parts.ElementAtOrDefault(1) ?? "unknown";
                var image = parts.ElementAtOrDefault(2) ?? "unknown";
                var ports = parts.ElementAtOrDefault(3);

                var icon = IsRunning(status) ? "🟢" : "🔴";

                sb.AppendLine($"{icon} {name}");
                sb.AppendLine($"• Status: {status}");
                sb.AppendLine($"• Image: {image}");
                sb.AppendLine($"• Ports: {(string.IsNullOrWhiteSpace(ports) ? "-" : ports)}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("📡 Networks");
        if (string.IsNullOrWhiteSpace(networks))
        {
            sb.AppendLine("• Proxy-related network tapılmadı.");
        }
        else
        {
            foreach (var line in SplitLines(networks).Take(10))
            {
                var parts = line.Split('|');
                sb.AppendLine($"• {parts.ElementAtOrDefault(0) ?? "unknown"} / {parts.ElementAtOrDefault(1) ?? "unknown"}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<string> GetSslStatusAsync(CancellationToken cancellationToken = default)
    {
        var certFiles = await BashAsync(
            "find /srv/homelab -type f \\( -name 'fullchain.pem' -o -name 'cert.pem' \\) 2>/dev/null | head -10",
            cancellationToken);

        var sb = new StringBuilder();

        sb.AppendLine("🔐 SSL Certificates");
        sb.AppendLine();

        if (string.IsNullOrWhiteSpace(certFiles))
        {
            sb.AppendLine("⚠️ SSL certificate file tapılmadı.");
            sb.AppendLine();
            sb.AppendLine("Qeyd:");
            sb.AppendLine("• Nginx Proxy Manager cert-ləri Docker volume içində ola bilər.");
            sb.AppendLine("• Domain expiry check üçün ayrıca domain registry əlavə edəcəyik.");
            sb.AppendLine();
            sb.AppendLine("Növbəti mərhələ:");
            sb.AppendLine("• planzy.org");
            sb.AppendLine("• *.planzy.org");
            sb.AppendLine("• alievfaig.dev");
            return sb.ToString().TrimEnd();
        }

        foreach (var cert in SplitLines(certFiles))
        {
            var expiry = await BashAsync(
                $"openssl x509 -enddate -noout -in {EscapeShellArg(cert)} 2>/dev/null | cut -d= -f2",
                cancellationToken);

            sb.AppendLine("📜 Certificate");
            sb.AppendLine($"• File: {cert}");
            sb.AppendLine($"• Expires: {(string.IsNullOrWhiteSpace(expiry) ? "unknown" : Clean(expiry))}");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    // Backward compatibility. Əgər köhnə kod haradasa GetDockerStatusAsync çağırırsa compile qırılmasın.
    public Task<string> GetDockerStatusAsync(CancellationToken cancellationToken = default)
    {
        return GetAllContainersAsync(cancellationToken);
    }

    private static async Task<string> DockerContainersAsync(
        string dockerCommand,
        CancellationToken cancellationToken)
    {
        return await BashAsync(
            $"{dockerCommand} --format '{{{{.Names}}}}|{{{{.Status}}}}|{{{{.Image}}}}|{{{{.Ports}}}}' 2>/dev/null",
            cancellationToken);
    }

    private static string FormatContainers(
        string title,
        string output,
        string emptyMessage)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return $"""
{title}

⚠️ {emptyMessage}

Serverdə yoxla:
docker ps -a
""";
        }

        var sb = new StringBuilder();

        sb.AppendLine(title);
        sb.AppendLine();

        foreach (var line in SplitLines(output).Take(20))
        {
            var parts = line.Split('|');

            var name = parts.ElementAtOrDefault(0) ?? "unknown";
            var status = parts.ElementAtOrDefault(1) ?? "unknown";
            var image = parts.ElementAtOrDefault(2) ?? "unknown";
            var ports = parts.ElementAtOrDefault(3);

            var icon = IsRunning(status) ? "🟢" : "🔴";

            sb.AppendLine($"{icon} {name}");
            sb.AppendLine($"• Status: {status}");
            sb.AppendLine($"• Image: {image}");
            sb.AppendLine($"• Ports: {(string.IsNullOrWhiteSpace(ports) ? "-" : ports)}");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static async Task<string> BashAsync(
        string command,
        CancellationToken cancellationToken)
    {
        return await RunAsync(
            "bash",
            ["-lc", command],
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

            return string.IsNullOrWhiteSpace(output) ? error : output;
        }
        catch
        {
            return "";
        }
    }

    private static string ParseOs(string osRelease)
    {
        var prettyName = SplitLines(osRelease)
            .FirstOrDefault(x => x.StartsWith("PRETTY_NAME=", StringComparison.OrdinalIgnoreCase));

        if (prettyName is null)
        {
            return "Linux";
        }

        return prettyName
            .Replace("PRETTY_NAME=", "", StringComparison.OrdinalIgnoreCase)
            .Trim()
            .Trim('"');
    }

    private static string ParseMemory(string freeOutput)
    {
        var line = SplitLines(freeOutput)
            .FirstOrDefault(x => x.StartsWith("Mem:", StringComparison.OrdinalIgnoreCase));

        if (line is null)
        {
            return "unknown";
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3)
        {
            return "unknown";
        }

        var total = parts[1];
        var used = parts[2];

        return $"{used} / {total}";
    }

    private static MemoryInfo? ParseMemoryDetailed(
        string freeOutput,
        string prefix)
    {
        var line = SplitLines(freeOutput)
            .FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (line is null)
        {
            return null;
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 4)
        {
            return null;
        }

        var total = parts.ElementAtOrDefault(1) ?? "unknown";
        var used = parts.ElementAtOrDefault(2) ?? "unknown";
        var free = parts.ElementAtOrDefault(3) ?? "unknown";
        var available = parts.ElementAtOrDefault(6) ?? "unknown";

        return new MemoryInfo(
            Total: total,
            Used: used,
            Free: free,
            Available: available);
    }

    private static string ParseDisk(string dfOutput)
    {
        var lines = SplitLines(dfOutput);

        if (lines.Count < 2)
        {
            return "unknown";
        }

        return ParseDfLine(lines[1]);
    }

    private static string ParseDfLine(string dfLine)
    {
        var parts = dfLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 5)
        {
            return "unknown";
        }

        var total = parts[1];
        var used = parts[2];
        var percent = parts[4];

        return $"{used} / {total} ({percent})";
    }

    private static string ParseLoad(string loadOutput)
    {
        var parts = loadOutput.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3)
        {
            return "unknown";
        }

        return $"{parts[0]}, {parts[1]}, {parts[2]}";
    }

    private static string ParseIp(string ipOutput)
    {
        var ip = ipOutput
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(x =>
                x.StartsWith("192.", StringComparison.OrdinalIgnoreCase) ||
                x.StartsWith("10.", StringComparison.OrdinalIgnoreCase) ||
                x.StartsWith("172.", StringComparison.OrdinalIgnoreCase) ||
                x.StartsWith("100.", StringComparison.OrdinalIgnoreCase));

        return ip ?? "unknown";
    }

    private static bool IsRunning(string status)
    {
        return status.StartsWith("Up", StringComparison.OrdinalIgnoreCase);
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : value.Trim();
    }

    private static IReadOnlyList<string> SplitLines(string value)
    {
        return value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

    private static string EscapeShellArg(string value)
    {
        return "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    }

    private sealed record MemoryInfo(
        string Total,
        string Used,
        string Free,
        string Available);
}