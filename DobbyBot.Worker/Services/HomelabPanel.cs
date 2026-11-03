using System.Diagnostics;
using System.Text.Json;
using DobbyBot.Worker.Commands;
using DobbyBot.Worker.Menus;
using DobbyBot.Worker.Services.Apps;
using static DobbyBot.Worker.Services.Apps.PanelText;

namespace DobbyBot.Worker.Services;

public sealed record FleetContainer(string Name, string State, string Status, string Image, string Ports)
{
    public bool Unhealthy => Status.Contains("unhealthy", StringComparison.OrdinalIgnoreCase);
    public bool NeedsAttention => Unhealthy || State is "restarting" or "dead";
}

public sealed class HomelabPanel(IAppProcessRunner runner)
{
    public static IReadOnlyList<FleetContainer> ParseContainers(string output) => output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line =>
        {
            using var doc = JsonDocument.Parse(line);
            var item = doc.RootElement;
            string Read(string name) => item.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
            return new FleetContainer(Read("Names"), Read("State"), Read("Status"), Read("Image"), Read("Ports"));
        }).ToArray();

    public async Task<BotResponse> HandleAsync(string callback, CancellationToken ct)
    {
        try
        {
            if (callback is "fleet:storage" or "fleet:networks" or "fleet:resources" or "fleet:diagnostics")
            {
                var (title, args) = callback switch
                {
                    "fleet:storage" => ("💾 Docker disk istifadəsi", new[] { "system", "df" }),
                    "fleet:networks" => ("🌐 Docker şəbəkələri", new[] { "network", "ls", "--format", "{{.Name}} | {{.Driver}} | {{.Scope}}" }),
                    "fleet:resources" => ("📈 Konteyner resursları", new[] { "stats", "--no-stream", "--format", "{{.Name}} | CPU {{.CPUPerc}} | RAM {{.MemUsage}} ({{.MemPerc}}) | NET {{.NetIO}}" }),
                    _ => ("🩺 Docker diaqnostikası", new[] { "info", "--format", "Engine: {{.ServerVersion}}\nKonteynerlər: {{.Containers}}\nİşləyir: {{.ContainersRunning}}\nDayanıb: {{.ContainersStopped}}\nCPU: {{.NCPU}}\nDriver: {{.Driver}}\nOS: {{.OperatingSystem}}" })
                };
                return new(Clip($"{title}\n\n{await runner.RunAsync("docker", args, ct)}\n\n{HostSummary()}"), Navigation(callback));
            }
            var containers = ParseContainers(await runner.RunAsync("docker", ["ps", "--all", "--format", "{{json .}}"], ct));
            var running = containers.Count(c => c.State == "running");
            var attention = containers.Where(c => c.NeedsAttention).ToArray();
            var stopped = containers.Where(c => c.State is "exited" or "created" or "paused").ToArray();
            if (callback == "fleet:issues")
            {
                var text = "🚨 Diqqət tələb edənlər\n\n" + (attention.Length == 0 ? "✅ Unhealthy, restarting və dead konteyner yoxdur." :
                    string.Join("\n", attention.Take(15).Select(c => $"🔴 {c.Name}\n{c.Status}")));
                text += "\n\n⏸️ Dayanmış / pauzada: " + stopped.Length + "\n" + string.Join("\n", stopped.Take(12).Select(c => $"• {c.Name} · {c.Status}"));
                text += "\n\nDayanmış konteynerlər planlı dayandırılmış ola bilər. Konteynerlər bölməsindən loqları yoxla.";
                return new(Clip(text), Navigation(callback));
            }
            return new($"🏠 Dobby · Homelab\n\n{(attention.Length == 0 ? "✅ Docker-də kritik vəziyyət görünmür" : $"⚠️ {attention.Length} konteyner diqqət tələb edir")}\n\n" +
                $"🐳 {containers.Count} konteyner\n🟢 {running} işləyir · ⏸️ {containers.Count - running} işləmir\n🩺 {containers.Count(c => c.Unhealthy)} unhealthy\n\n{HostSummary()}\n\nGöstəricilər panel açıldıqda yenilənir.", DobbyMenus.MainMenu());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            return new(Clip($"⚠️ Docker məlumatı alınmadı\n\n{ex.Message}\n\n{HostSummary()}\n\nDigər bölmələrdən istifadə edə bilərsən."), DobbyMenus.MainMenu());
        }
    }

    private static string HostSummary()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var text = $"🖥️ {Environment.MachineName}\n⏱️ Sistem uptime: {(int)uptime.TotalDays} gün {uptime.Hours} saat {uptime.Minutes} dəq";
        if (OperatingSystem.IsLinux())
        {
            try
            {
                text += "\n🔥 Load: " + string.Join(" / ", File.ReadAllText("/proc/loadavg").Split(' ').Take(3));
                var memory = File.ReadAllLines("/proc/meminfo").Select(l => l.Split(':', 2)).ToDictionary(p => p[0], p => p[1].Trim().Split(' ')[0]);
                var total = long.Parse(memory["MemTotal"]);
                var available = long.Parse(memory["MemAvailable"]);
                text += $"\n🧠 RAM: {(total - available) / 1048576d:F1} / {total / 1048576d:F1} GiB";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or KeyNotFoundException) { text += "\nRAM / Load məlumatı əlçatan deyil."; }
        }
        using var process = Process.GetCurrentProcess();
        var started = DateTimeOffset.UtcNow - process.StartTime.ToUniversalTime();
        return text + $"\n🤖 Bot uptime: {(int)started.TotalHours} saat {started.Minutes} dəq";
    }

    private static Bot.TelegramInlineKeyboardMarkup Navigation(string callback) => Keys(
        [Button("🔄 Yenilə", callback), Button("🚨 Problemlər", "fleet:issues")],
        [Button("🐳 Konteynerlər", "menu:docker"), Button("🏠 Əsas panel", "menu:main")]);
}
