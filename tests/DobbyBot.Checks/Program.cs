using DobbyBot.Worker.Services.Apps;
using Microsoft.Extensions.Options;

var root = Path.Combine(Path.GetTempPath(), "dobby-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    File.WriteAllText(Path.Combine(root, "compose.yaml"), "services: {}");
    var runner = new FakeRunner();
    var options = Options.Create(new AppManagementOptions { Repositories = [new() { Name = "Planzy", ProjectName = "planzy", Directory = root }] });
    var service = new AppManagementService(options, runner);
    var panel = new AppsPanel(service);
    var apps = await service.DiscoverAsync(default);
    Check(apps.Count == 2 && apps.Any(a => a.Name == "Planzy"), "Compose discovery and configured missing repo");
    var app = apps.Single(a => a.Name == "Planzy");
    Check(app.Id.Length == 16, "Callback identity is bounded");
    var list = await panel.HandleAsync(1, "menu:apps", default);
    Check(list.Text.Contains("2 tətbiq"), "Dynamic app list");
    var confirm = await panel.HandleAsync(1, $"managed:{app.Id}:update", default);
    var callback = confirm.ReplyMarkup!.InlineKeyboard[0][0].CallbackData;
    var wrongUser = await panel.HandleAsync(2, callback, default);
    Check(wrongUser.Text.Contains("Təsdiq vaxtı"), "Confirmation bound to user");
    var result = await panel.HandleAsync(1, callback, default);
    Check(result.Text.Contains("Git yeniləndi"), "Full update completes");
    Check(runner.Calls.Any(c => c.Contains("pull --ff-only origin main")), "Fast forward update");
    Check(runner.Calls.Any(c => c.Contains("--wait-timeout 120")), "Deployment waits for health");
    var count = runner.Calls.Count(c => c.Contains("pull --ff-only"));
    await panel.HandleAsync(1, callback, default);
    Check(runner.Calls.Count(c => c.Contains("pull --ff-only")) == count, "Confirmation cannot replay");
    runner.Dirty = true;
    var blocked = await service.OperateAsync(app, "update", default);
    Check(blocked.Contains("Yerli dəyişikliklər"), "Dirty repo blocks deployment");
    runner.Dirty = false;
    runner.Branch = "other";
    Check((await service.OperateAsync(app, "update", default)).Contains("Aktiv branch"), "Unexpected branch blocks deployment");
    runner.Branch = "main";
    runner.FailBuild = true;
    var before = runner.Calls.Count;
    try { await service.OperateAsync(app, "update", default); throw new Exception("Build failure was ignored"); }
    catch (InvalidOperationException) { }
    Check(!runner.Calls.Skip(before).Any(c => c.Contains(" up ")), "Build failure prevents deployment");
        var unmanaged = apps.Single(a => a.Repository is null);
    try { await service.OperateAsync(unmanaged, "stop", default); throw new Exception("Unconfigured app was managed"); }
    catch (InvalidOperationException) { Console.WriteLine("PASS Unconfigured app cannot be changed"); }
    runner.FailBuild = false;
    Check((await service.OperateAsync(app, "start", default)) == "ok", "Failed operation releases lock");
    Check(DobbyBot.Worker.Menus.DobbyMenus.MainMenu().InlineKeyboard.All(r => r.Length == 2), "Dashboard uses compact grid");
    Console.WriteLine("All 14 checks passed.");
}
finally { Directory.Delete(root, true); }

static void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    Console.WriteLine("PASS " + label);
}
sealed class FakeRunner : IAppProcessRunner
{
    public List<string> Calls { get; } = [];
    public bool Dirty { get; set; }
    public bool FailBuild { get; set; }
    public string Branch { get; set; } = "main";
    public Task<string> RunAsync(string executable, string[] arguments, CancellationToken ct, string[]? alternate = null)
    {
        var args = alternate ?? arguments;
        var text = string.Join(" ", args);
        Calls.Add(text);
        if (args.Contains("build") && FailBuild) throw new InvalidOperationException("Build failed");
        return Task.FromResult(text.Contains("ls --all") ? "[{\"Name\":\"other\",\"Status\":\"running(1)\"}]" :
            text.Contains("--porcelain") ? (Dirty ? " M file" : "") : text.Contains("--show-current") ? Branch : "ok");
    }
}

