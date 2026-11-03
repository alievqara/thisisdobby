using System.Collections.Concurrent;
using DobbyBot.Worker.Bot;
using DobbyBot.Worker.Commands;

namespace DobbyBot.Worker.Services.Apps;

public sealed class AppsPanel(AppManagementService service)
{
    private sealed record Pending(string AppId, string Action, DateTimeOffset Expires);
    private readonly ConcurrentDictionary<(long User, string Token), Pending> _pending = new();
    private static TelegramInlineKeyboardButton Button(string text, string callback) => new() { Text = text, CallbackData = callback };
    private static TelegramInlineKeyboardMarkup Keys(params TelegramInlineKeyboardButton[][] rows) => new() { InlineKeyboard = rows };
    private static string Clip(string text) => text.Length > 3400 ? text[..3400] + "\n…" : text;

    public async Task<BotResponse> HandleAsync(long user, string callback, CancellationToken ct)
    {
        try
        {
            var apps = await service.DiscoverAsync(ct);
            var parts = callback.Split(':');
            if (callback == "menu:apps" || parts.Length < 3)
            {
                var rows = apps.Select(a => new[] { Button($"{(a.Status.Contains("running") ? "🟢" : "⚪")} {a.Name}", $"managed:{a.Id}:status") }).ToList();
                rows.Add([Button("↻ Yenidən aşkarla", "menu:apps"), Button("⌂ Panel", "menu:main")]);
                return new BotResponse($"TƏTBİQLƏR\n━━━━━━━━━━━━━━━━━━━━\n{apps.Count} tətbiq • Docker Compose\n\n" + Clip(string.Join("\n", apps.Take(25).Select(a => $"{a.Name} · {a.Status}"))) + "\n\nTətbiqi seçərək status, loqlar və idarəetməni aç.", new() { InlineKeyboard = rows.ToArray() });
            }
            var app = apps.FirstOrDefault(a => a.Id == parts[1]);
            if (app is null) return new BotResponse("Tətbiq artıq mövcud deyil. Siyahını yenilə.", Keys([Button("↻ Tətbiqlər", "menu:apps")]));
            var action = parts[2];
            string body;
            if (action is "update" or "restart" or "stop" or "start")
            {
                if (app.Repository is null) throw new InvalidOperationException("Bu tətbiq üçün repo konfiqurasiyası əlavə edilməlidir.");
                foreach (var stale in _pending.Where(p => p.Value.Expires < DateTimeOffset.UtcNow).Select(p => p.Key)) _pending.TryRemove(stale, out _);
                var token = Guid.NewGuid().ToString("N")[..12];
                _pending[(user, token)] = new(app.Id, action, DateTimeOffset.UtcNow.AddMinutes(2));
                return new BotResponse($"ƏMƏLİYYATI TƏSDİQLƏ\n━━━━━━━━━━━━━━━━━━━━\n{app.Name} · {action}\n\n" + (action == "update" ? $"origin/{app.Repository.Branch} → git pull → image build/pull → Compose up → health yoxlaması\n\nQısa fasilə mümkündür. Avtomatik rollback yoxdur." : "Tətbiqin işləmə vəziyyəti dəyişəcək."), Keys([Button("✓ Təsdiqlə", $"managed:{app.Id}:confirm:{token}"), Button("Ləğv et", $"managed:{app.Id}:status")]));
            }
            if (action == "confirm")
            {
                if (parts.Length != 4 || !_pending.TryRemove((user, parts[3]), out var pending) || pending.AppId != app.Id || pending.Expires < DateTimeOffset.UtcNow)
                    body = "Təsdiq vaxtı bitib və ya artıq istifadə olunub. Əməliyyatı yenidən seç.";
                else body = await service.OperateAsync(app, pending.Action, ct);
            }
            else body = await service.ReadAsync(app, action, ct);
            var id = app.Id;
            var buttons = new List<TelegramInlineKeyboardButton[]> { new[] { Button("◎ Status / Health", $"managed:{id}:status"), Button("≡ Loqlar", $"managed:{id}:logs") } };
            if (app.Repository is not null)
            {
                buttons.Add([Button("⑂ Git versiyası", $"managed:{id}:git"), Button("↑ GitHub-dan yenilə", $"managed:{id}:update")]);
                buttons.Add([Button("▶ Başlat", $"managed:{id}:start"), Button("↻ Restart", $"managed:{id}:restart"), Button("■ Dayandır", $"managed:{id}:stop")]);
            }
            buttons.Add([Button("‹ Tətbiqlər", "menu:apps"), Button("⌂ Panel", "menu:main")]);
            return new BotResponse(Clip($"{app.Name.ToUpperInvariant()}\n━━━━━━━━━━━━━━━━━━━━\n{app.Status}\n\n{body}"), new() { InlineKeyboard = buttons.ToArray() });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            return new BotResponse(Clip("⚠️ Əməliyyat tamamlanmadı\n\n" + ex.Message + "\n\nYeniləmə yarımçıq qalıbsa repo və Compose vəziyyətini yoxla."), Keys([Button("↻ Tətbiqlər", "menu:apps"), Button("⌂ Panel", "menu:main")]));
        }
    }
}


