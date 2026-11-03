using System.Collections.Concurrent;
using DobbyBot.Worker.Commands;
using static DobbyBot.Worker.Services.Apps.PanelText;

namespace DobbyBot.Worker.Services.Apps;

public sealed class AppsPanel(AppManagementService service, AppOperations operations)
{
    private sealed record Pending(string AppId, string Action, DateTimeOffset Expires);
    private readonly ConcurrentDictionary<(long User, string Token), Pending> _pending = new();

    public async Task<BotResponse> HandleAsync(long user, string callback, CancellationToken ct, long? chat = null)
    {
        try
        {
            var parts = callback.Split(':');
            // Confirm before discovery: a consumed token can never start a second job.
            if (parts.Length == 4 && parts[0] == "managed" && parts[2] == "confirm")
            {
                if (!_pending.TryRemove((user, parts[3]), out var pending) || pending.AppId != parts[1] || pending.Expires < DateTimeOffset.UtcNow)
                    return new("⌛ Təsdiq vaxtı bitib və ya artıq istifadə olunub.", Keys([Button("📱 Tətbiqlər", "menu:apps")]));
                var current = (await service.DiscoverAsync(ct)).FirstOrDefault(a => a.Id == pending.AppId)
                    ?? throw new InvalidOperationException("Tətbiq artıq mövcud deyil.");
                var job = operations.Enqueue(user, chat ?? user, current, pending.Action);
                return new($"🕒 {current.Name}\n\n{PanelText.Action(pending.Action)} növbəyə əlavə edildi.\nNəticə bu söhbətə göndəriləcək. Botdan istifadə etməyə davam edə bilərsən.\n\nƏməliyyat ID: {job.Id}",
                    Keys([Button("📋 Əməliyyatın gedişi", "operations:list"), Button("📱 Tətbiqlər", "menu:apps")]));
            }

            var apps = await service.DiscoverAsync(ct);
            if (callback.StartsWith("menu:apps", StringComparison.Ordinal))
            {
                var page = parts.Length == 3 && int.TryParse(parts[2], out var n) ? n : 0;
                const int size = 8;
                var pages = Math.Max(1, (apps.Count + size - 1) / size);
                page = Math.Clamp(page, 0, pages - 1);
                var items = apps.Skip(page * size).Take(size).ToArray();
                var rows = items.Select(a => new[] { Button($"{StatusIcon(a.Status)} {Clip(a.Name, 45)}", $"managed:{a.Id}:status") }).ToList();
                if (pages > 1) rows.Add([Button("⬅️ Əvvəlki", $"menu:apps:{Math.Max(0, page - 1)}"), Button("➡️ Sonrakı", $"menu:apps:{Math.Min(pages - 1, page + 1)}")]);
                rows.Add([Button("🔄 Yenilə", $"menu:apps:{page}"), Button("📋 Əməliyyatlar", "operations:list")]);
                rows.Add([Button("🏠 Əsas panel", "menu:main")]);
                return new($"📱 Tətbiqlər\n\n{apps.Count} tətbiq · Səhifə {page + 1}/{pages}\n\n" +
                    (items.Length == 0 ? "Compose tətbiqi tapılmadı." : string.Join("\n", items.Select(a => $"{StatusIcon(a.Status)} {Clip(a.Name, 45)} · {Clip(a.Status, 90)}"))) +
                    "\n\n🟢 İşləyir · 🟡 Qarışıq / yoxlanır · ⚪ Dayanıb\nGitHub idarəetməsi yalnız repo sazlanan tətbiqlərdə açılır.", new() { InlineKeyboard = rows.ToArray() });
            }
            if (parts.Length < 3) throw new InvalidOperationException("Düymə tanınmadı. Menyunu yenilə.");
            var app = apps.FirstOrDefault(a => a.Id == parts[1]) ?? throw new InvalidOperationException("Tətbiq artıq mövcud deyil.");
            var action = parts[2];
            if (action == "cancel")
            {
                foreach (var entry in _pending.Where(p => p.Key.User == user && p.Value.AppId == app.Id)) _pending.TryRemove(entry.Key, out _);
                action = "status";
            }
            if (action is "update" or "restart" or "stop" or "start")
            {
                if (app.Repository is null) throw new InvalidOperationException("Bu tətbiq üçün repo konfiqurasiyası əlavə edilməlidir.");
                foreach (var stale in _pending.Where(p => p.Value.Expires < DateTimeOffset.UtcNow || (p.Key.User == user && p.Value.AppId == app.Id))) _pending.TryRemove(stale.Key, out _);
                var token = Guid.NewGuid().ToString("N")[..12];
                _pending[(user, token)] = new(app.Id, action, DateTimeOffset.UtcNow.AddMinutes(2));
                return new($"⚠️ Əməliyyatı təsdiqlə\n\n📱 {app.Name}\n🛠️ {PanelText.Action(action)}\n\n" +
                    (action == "update" ? $"Branch: {app.Repository.Branch}\nKod və image-lər yenilənəcək, tətbiq yenidən başladılacaq. Qısa fasilə mümkündür. Avtomatik rollback yoxdur." : "Tətbiqin işləmə vəziyyəti dəyişəcək.") + "\n\nTəsdiq 2 dəqiqə etibarlıdır.",
                    Keys([Button("✅ Təsdiqlə", $"managed:{app.Id}:confirm:{token}"), Button("❌ Ləğv et", $"managed:{app.Id}:cancel")]));
            }
            var body = await service.ReadAsync(app, action, ct);
            var id = app.Id;
            var buttons = new List<Bot.TelegramInlineKeyboardButton[]> { new[] { Button("📊 Status", $"managed:{id}:status"), Button("📜 Loqlar", $"managed:{id}:logs") } };
            if (app.Repository is not null)
            {
                buttons.Add([Button("🔎 Yeniləmə yoxla", $"managed:{id}:check"), Button("🌿 Git versiyası", $"managed:{id}:git")]);
                buttons.Add([Button("🚀 GitHub-dan yenilə", $"managed:{id}:update")]);
                buttons.Add([Button("▶️ Başlat", $"managed:{id}:start"), Button("🔄 Restart", $"managed:{id}:restart"), Button("⏹️ Dayandır", $"managed:{id}:stop")]);
            }
            buttons.Add([Button("⬅️ Tətbiqlər", "menu:apps"), Button("🏠 Əsas panel", "menu:main")]);
            return new(Clip($"📱 {app.Name}\n{StatusIcon(app.Status)} {app.Status}\n\n{body}"), new() { InlineKeyboard = buttons.ToArray() });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            return new(Clip("⚠️ Əməliyyat tamamlanmadı\n\n" + ex.Message), Keys([Button("🔄 Tətbiqlər", "menu:apps"), Button("🏠 Əsas panel", "menu:main")]));
        }
    }

    public static string StatusIcon(string status)
    {
        var parts = status.ToLowerInvariant().Split(',', StringSplitOptions.TrimEntries);
        return parts.Length == 1 && parts[0].StartsWith("running(", StringComparison.Ordinal) ? "🟢" :
            parts.Any(p => p.StartsWith("running(", StringComparison.Ordinal)) ? "🟡" : "⚪";
    }
}
