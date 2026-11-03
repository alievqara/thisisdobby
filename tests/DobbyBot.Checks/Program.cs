using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using DobbyBot.Worker.Services;
using DobbyBot.Worker.Services.Apps;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var passed = 0;
var root = Path.Combine(Path.GetTempPath(), "dobby-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    File.WriteAllText(Path.Combine(root, "compose.yaml"), "services: {}");
    var runner = new FakeRunner();
    var service = new AppManagementService(Options.Create(new AppManagementOptions
    {
        Repositories = [new() { Name = "Planzy", ProjectName = "planzy", Directory = root }]
    }), runner);
    var notifications = new Notifications();
    using var operations = new AppOperations(service, notifications, NullLogger<AppOperations>.Instance);
    var panel = new AppsPanel(service, operations);
    var apps = await service.DiscoverAsync(default);
    Check(apps.Count == 2 && apps.Any(a => a.Name == "Planzy"), "Compose discovery includes configured missing repo");
    var app = apps.Single(a => a.Name == "Planzy");
    Check(app.Id.Length == 16, "Bounded callback identity");
    var list = await panel.HandleAsync(1, "menu:apps", default);
    Check(list.Text.Contains("2 tətbiq"), "Dynamic app list");
    var confirm = await panel.HandleAsync(1, $"managed:{app.Id}:update", default);
    var callback = confirm.ReplyMarkup!.InlineKeyboard[0][0].CallbackData;
    Check((await panel.HandleAsync(2, callback, default)).Text.Contains("Təsdiq vaxtı"), "Confirmation bound to user");
    var response = await panel.HandleAsync(1, callback, default, -12345);
    Check(response.Text.Contains("növbəyə əlavə"), "Confirmation queues work without blocking");
    Check(!runner.Calls.Any(c => c.Contains("pull --ff-only")), "Deployment does not run in callback");
    ExpectFailure(() => operations.Enqueue(1, 1, app, "start"), "Duplicate pending job rejected");
    await panel.HandleAsync(1, callback, default);
    Check(operations.History(1).Count == 1, "Confirmation cannot replay");
    Check(operations.History(2).Count == 0, "Job history bound to user");
    await operations.StartAsync(default);
    var notification = await notifications.Next();
    Check(notification.Chat == -12345 && notification.Text.Contains("Git yeniləndi"), "Completion returns to originating chat");
    Check(operations.History(1).Single().State == "done", "Job completes and keeps history");
    Check(runner.Calls.Any(c => c.Contains("pull --ff-only origin main")), "Fast forward update");
    Check(runner.Calls.Count(c => c.Contains("build --pull")) == 1 && runner.Calls.Any(c => c.Contains("--no-build --wait")), "Images built once and health awaited");
    runner.Dirty = true;
    await ExpectAsync(() => service.OperateAsync(app, "update", default), "Dirty repo blocks deployment");
    operations.Enqueue(1, 1, app, "update");
    Check((await notifications.Next()).Text.StartsWith("❌"), "Rejected update reported as failed job");
    runner.Dirty = false;
    runner.Branch = "other";
    await ExpectAsync(() => service.OperateAsync(app, "update", default), "Unexpected branch blocks deployment");
    runner.Branch = "main";
    runner.FailBuild = true;
    var before = runner.Calls.Count;
    await ExpectAsync(() => service.OperateAsync(app, "update", default), "Build failure propagates");
    Check(!runner.Calls.Skip(before).Any(c => c.Contains(" up ")), "Build failure prevents deployment");
    runner.FailBuild = false;
    await ExpectAsync(() => service.OperateAsync(apps.Single(a => a.Repository is null), "stop", default), "Unconfigured app cannot change");
    Check(await service.OperateAsync(app, "start", default) == "ok", "Failure releases operation lock");
    before = runner.Calls.Count;
    Check((await service.ReadAsync(app, "check", default)).Contains("2 yeni commit"), "Update check reports remote difference");
    Check(!runner.Calls.Skip(before).Any(c => c.Contains("pull ") || c.Contains(" up ")), "Update check does not deploy");
    before = runner.Calls.Count;
    await service.ReadAsync(apps.Single(a => a.Repository is null), "status", default);
    Check(runner.Calls.Skip(before).Any(c => c.Contains("label=com.docker.compose.project=other")), "Unconfigured app status uses Docker labels");
    await service.ReadAsync(app, "logs", default);
    Check(runner.Calls.Any(c => c.StartsWith("logs --timestamps --tail 15 abc123")), "Logs read without local compose file");
    confirm = await panel.HandleAsync(1, $"managed:{app.Id}:stop", default);
    callback = confirm.ReplyMarkup!.InlineKeyboard[0][0].CallbackData;
    await panel.HandleAsync(1, $"managed:{app.Id}:cancel", default);
    Check((await panel.HandleAsync(1, callback, default)).Text.Contains("Təsdiq vaxtı"), "Cancel invalidates confirmation");
    runner.AppCount = 20;
    list = await panel.HandleAsync(1, "menu:apps:1", default);
    Check(list.Text.Contains("Səhifə 2/3") && list.ReplyMarkup!.InlineKeyboard.Count(r => r[0].CallbackData.StartsWith("managed:")) == 8, "App list pagination");
    Check(list.ReplyMarkup!.InlineKeyboard.SelectMany(r => r).All(b => Encoding.UTF8.GetByteCount(b.CallbackData) <= 64), "Callbacks fit Telegram limit");
    Check(AppsPanel.StatusIcon("running(1), exited(1)") == "🟡" && AppsPanel.StatusIcon("running(2)") == "🟢", "Mixed state never shown as all running");
    var fleet = new HomelabPanel(runner);
    var dashboard = await fleet.HandleAsync("menu:main", default);
    Check(dashboard.Text.Contains("3 konteyner") && dashboard.Text.Contains("1 konteyner diqqət"), "Dashboard reports fleet and health");
    var issues = await fleet.HandleAsync("fleet:issues", default);
    Check(issues.Text.Contains("api") && issues.Text.Contains("planlı dayandırılmış"), "Issues distinguish unhealthy and stopped");
    runner.DockerOffline = true;
    dashboard = await fleet.HandleAsync("menu:main", default);
    Check(dashboard.Text.Contains("Docker məlumatı alınmadı") && dashboard.ReplyMarkup is not null, "Docker outage preserves navigation");
    Check(DobbyBot.Worker.Menus.DobbyMenus.DownloaderMenu().InlineKeyboard.SelectMany(r => r).All(b => b.CallbackData == "menu:main"), "Unfinished downloader not presented as working");
    var clipped = PanelText.Clip(new string('a', 3399) + "🟢xyz");
    Check(!char.IsHighSurrogate(clipped.Split('\n')[0][^1]), "Message clipping preserves emoji");
    runner.DockerOffline = false;
    runner.BuildGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    runner.BuildStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    operations.Enqueue(1, 1, app, "update");
    await runner.BuildStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
    ExpectFailure(() => operations.Enqueue(1, 1, app, "stop"), "Running app cannot receive duplicate operation");
    Check(await service.ReadAsync(app, "status", default) == "ok", "Status stays responsive during build");
    var second = app with { Id = "second", Name = "Second", Project = "second" };
    operations.Enqueue(1, 1, second, "start");
    Check(operations.History(1).First(j => j.App.Id == second.Id).State == "queued", "Second app waits behind running deployment");
    runner.BuildGate.SetResult();
    await notifications.Next();
    await notifications.Next();
    Check(operations.History(1).Take(2).All(j => j.State == "done"), "Queued operations execute in sequence");
    await operations.StopAsync(default);

    // Exercise the actual process runner, including stderr and cancellation.
    var process = new AppProcessRunner();
    Check((await process.RunAsync("git", ["--version"], default)).StartsWith("git version"), "Real process runner reads stdout");
    await ExpectAsync(() => process.RunAsync("git", ["-C", root, "status"], default), "Real process errors propagate");
    Console.WriteLine($"All {passed} checks passed.");
}
finally { Directory.Delete(root, true); }

void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    passed++;
    Console.WriteLine("PASS " + label);
}
void ExpectFailure(Action action, string label)
{
    try { action(); } catch (InvalidOperationException) { Check(true, label); return; }
    throw new Exception(label);
}
async Task ExpectAsync(Func<Task<string>> action, string label)
{
    try { await action(); } catch (InvalidOperationException) { Check(true, label); return; }
    throw new Exception(label);
}
sealed class Notifications : IOperationNotifications
{
    private readonly Channel<(long Chat, string Text)> _messages = Channel.CreateUnbounded<(long, string)>();
    public Task SendAsync(long chatId, string text, CancellationToken ct) { _messages.Writer.TryWrite((chatId, text)); return Task.CompletedTask; }
    public async Task<(long Chat, string Text)> Next()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await _messages.Reader.ReadAsync(deadline.Token);
    }
}
sealed class FakeRunner : IAppProcessRunner
{
    public List<string> Calls { get; } = [];
    public TaskCompletionSource? BuildGate { get; set; }
    public TaskCompletionSource? BuildStarted { get; set; }
    public bool Dirty { get; set; }
    public bool FailBuild { get; set; }
    public bool DockerOffline { get; set; }
    public int AppCount { get; set; } = 1;
    public string Branch { get; set; } = "main";
    private async Task<string> WaitForBuild(CancellationToken ct)
    {
        await BuildGate!.Task.WaitAsync(ct);
        return "ok";
    }
    public Task<string> RunAsync(string executable, string[] arguments, CancellationToken ct, string[]? alternate = null)
    {
        var args = alternate ?? arguments;
        var text = string.Join(" ", args);
        Calls.Add(text);
        if (executable == "docker" && DockerOffline) throw new InvalidOperationException("Docker offline");
        if (args.Contains("build") && FailBuild) throw new InvalidOperationException("Build failed");
        if (args.Contains("build") && BuildGate is not null)
        {
            BuildStarted?.TrySetResult();
            return WaitForBuild(ct);
        }
        if (text.Contains("ls --all")) return Task.FromResult(JsonSerializer.Serialize(Enumerable.Range(0, AppCount).Select(n => new { Name = n == 0 ? "other" : $"app{n}", Status = "running(1)" })));
        if (text.Contains("{{json .}}")) return Task.FromResult(string.Join('\n',
            "{\"Names\":\"api\",\"State\":\"running\",\"Status\":\"Up 1 hour (unhealthy)\"}",
            "{\"Names\":\"db\",\"State\":\"running\",\"Status\":\"Up 1 hour (healthy)\"}",
            "{\"Names\":\"worker\",\"State\":\"exited\",\"Status\":\"Exited (0)\"}"));
        return Task.FromResult(text.Contains("--porcelain") ? (Dirty ? " M file" : "") :
            text.Contains("--show-current") ? Branch : text.Contains("rev-list") ? "0\t2" : text.Contains("{{.ID}}") ? "abc123" : "ok");
    }
}
