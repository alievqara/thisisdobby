using DobbyBot.Worker.Bot;
using DobbyBot.Worker.Commands;
using DobbyBot.Worker.Execution;
using DobbyBot.Worker.Modules.DevTasks;
using DobbyBot.Worker.Modules.Downloader;
using DobbyBot.Worker.Options;
using DobbyBot.Worker.Security;
using DobbyBot.Worker.Services;
using DobbyBot.Worker.Services.Ai;
using DobbyBot.Worker.Services.Docker;
using DobbyBot.Worker.Services.SystemPower;
using DobbyBot.Worker.State;
using DobbyBot.Worker.TextRouting;
using DotNetEnv;
using Microsoft.Extensions.Options;

Env.TraversePath().Load();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<DobbyBotOptions>(
    builder.Configuration.GetSection("DobbyBot"));

builder.Services.Configure<AiTaskOptions>(
    builder.Configuration.GetSection("AiTask"));
builder.Services.Configure<LocalAiOptions>(
    builder.Configuration.GetSection("LocalAi"));
builder.Services.AddHttpClient<ILocalAiService, OllamaLocalAiService>((serviceProvider, httpClient) =>
{
    var options = serviceProvider
        .GetRequiredService<IOptions<LocalAiOptions>>()
        .Value;

    httpClient.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
    httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.AddHttpClient<TelegramGateway>();

builder.Services.AddSingleton<IAdminGuard, AdminGuard>();
builder.Services.AddSingleton<ICommandExecutor, CommandExecutor>();

builder.Services.AddSingleton<IServerStatusService, ServerStatusService>();
builder.Services.AddSingleton<IServiceControlService, ServiceControlService>();

builder.Services.AddSingleton<IDownloaderInputParser, DownloaderInputParser>();
builder.Services.AddSingleton<IUserStateService, InMemoryUserStateService>();

builder.Services.AddSingleton<ITextMessageRouter, TextMessageRouter>();

builder.Services.AddSingleton<IDevTaskService, DevTaskService>();
builder.Services.AddSingleton<IAgentRunnerService, ClaudeCodeRunnerService>();
builder.Services.AddSingleton<ITaskReportFormatter, TaskReportFormatter>();

builder.Services.AddSingleton<IDockerService, DockerService>();
builder.Services.AddSingleton<ISystemPowerService, SystemPowerService>();


builder.Services.AddSingleton<CommandRouter>();

builder.Services.AddHostedService<DobbyBotWorker>();

var host = builder.Build();

host.Run();