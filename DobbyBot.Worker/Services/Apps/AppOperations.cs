using System.Threading.Channels;
using DobbyBot.Worker.Bot;
using DobbyBot.Worker.Commands;

namespace DobbyBot.Worker.Services.Apps;

public interface IOperationNotifications
{
    Task SendAsync(long chatId, string text, CancellationToken ct);
}

public sealed class TelegramOperationNotifications(TelegramGateway telegram) : IOperationNotifications
{
    public async Task SendAsync(long chatId, string text, CancellationToken ct) =>
        await telegram.SendMessageAsync(chatId, PanelText.Clip(text), null, ct);
}

public sealed record AppJob(string Id, long User, long Chat, HomelabApp App, string Action,
    string State, string Detail, DateTimeOffset Created);

public sealed class AppOperations(AppManagementService service, IOperationNotifications notifications,
    ILogger<AppOperations> logger) : BackgroundService
{
    private readonly object _gate = new();
    private readonly List<AppJob> _jobs = [];
    private readonly Channel<string> _queue = Channel.CreateBounded<string>(8);

    public AppJob Enqueue(long user, long chat, HomelabApp app, string action)
    {
        lock (_gate)
        {
            if (_jobs.Any(j => j.App.Id == app.Id && j.State is "queued" or "running"))
                throw new InvalidOperationException("Bu tətbiq üçün əməliyyat artıq növbədədir və ya işləyir.");
            var job = new AppJob(Guid.NewGuid().ToString("N")[..12], user, chat, app, action,
                "queued", "Növbədə gözləyir", DateTimeOffset.UtcNow);
            _jobs.Add(job);
            if (!_queue.Writer.TryWrite(job.Id))
            {
                _jobs.Remove(job);
                throw new InvalidOperationException("Əməliyyat növbəsi doludur. Bir az sonra yoxla.");
            }
            while (_jobs.Count > 60)
            {
                var oldest = _jobs.FindIndex(j => j.State is not ("queued" or "running"));
                if (oldest < 0) break;
                _jobs.RemoveAt(oldest);
            }
            return job;
        }
    }

    public IReadOnlyList<AppJob> History(long user)
    {
        lock (_gate) return _jobs.Where(j => j.User == user).Reverse().ToArray();
    }

    private AppJob Set(string id, string state, string detail)
    {
        lock (_gate)
        {
            var index = _jobs.FindIndex(j => j.Id == id);
            return _jobs[index] = _jobs[index] with { State = state, Detail = detail };
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var id in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            var job = Set(id, "running", "İcra başlayır");
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                deadline.CancelAfter(TimeSpan.FromMinutes(30));
                var result = await service.OperateAsync(job.App, job.Action, deadline.Token,
                    stage => Set(id, "running", stage));
                job = Set(id, "done", string.IsNullOrWhiteSpace(result) ? "Əməliyyat tamamlandı." : result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                Set(id, "failed", "Bot dayandırıldı. Tətbiqin vəziyyətini yoxla.");
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("App operation {JobId} failed: {ErrorType}", id, ex.GetType().Name);
                job = Set(id, "failed", ex is OperationCanceledException
                    ? "Vaxt limiti bitdi. Repo və konteyner vəziyyətini yoxla." : ex.Message);
            }
            try
            {
                await notifications.SendAsync(job.Chat,
                    $"{(job.State == "done" ? "✅" : "❌")} {job.App.Name} · {PanelText.Action(job.Action)}\n\n{job.Detail}\n\nƏməliyyat ID: {job.Id}", stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Operation notification failed: {ErrorType}", ex.GetType().Name);
            }
        }
    }

    public BotResponse Panel(long user)
    {
        var history = History(user).Take(10).ToArray();
        var text = "📋 Əməliyyatlar\n\n" + (history.Length == 0 ? "Hələ əməliyyat yoxdur." :
            string.Join("\n\n", history.Select(j => $"{Icon(j.State)} {j.App.Name} · {PanelText.Action(j.Action)}\n{j.Detail}\n{j.Created:yyyy-MM-dd HH:mm} UTC · {j.Id}")));
        return new(PanelText.Clip(text) + "\n\nTarixçə bot başladıqdan sonrakı əməliyyatları göstərir.", PanelText.Keys(
            [PanelText.Button("🔄 Yenilə", "operations:list"), PanelText.Button("🏠 Əsas panel", "menu:main")]));
    }

    private static string Icon(string state) => state switch { "done" => "✅", "failed" => "❌", "running" => "⏳", _ => "🕒" };
}

public static class PanelText
{
    public static string Clip(string value, int limit = 3400)
    {
        if (value.Length <= limit) return value;
        var end = char.IsHighSurrogate(value[limit - 1]) ? limit - 1 : limit;
        return value[..end] + "\n…";
    }
    public static TelegramInlineKeyboardButton Button(string text, string callback) => new() { Text = text, CallbackData = callback };
    public static TelegramInlineKeyboardMarkup Keys(params TelegramInlineKeyboardButton[][] rows) => new() { InlineKeyboard = rows };
    public static string Action(string action) => action switch
    {
        "update" => "GitHub-dan yeniləmə", "start" => "Başlatma", "stop" => "Dayandırma", "restart" => "Yenidən başlatma", _ => action
    };
}
