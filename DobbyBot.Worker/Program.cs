using DobbyBot.Worker.Bot;
using DobbyBot.Worker.Commands;
using DobbyBot.Worker.Execution;
using DobbyBot.Worker.Modules.Downloader;
using DobbyBot.Worker.Options;
using DobbyBot.Worker.Security;
using DobbyBot.Worker.Services;
using DobbyBot.Worker.Services.Docker;
using DobbyBot.Worker.Services.SystemPower;
using DobbyBot.Worker.State;
using DotNetEnv;

Env.TraversePath().Load();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<DobbyBotOptions>(
    builder.Configuration.GetSection("DobbyBot"));

builder.Services.AddHttpClient<TelegramGateway>();

builder.Services.AddSingleton<IAdminGuard, AdminGuard>();
builder.Services.AddSingleton<ICommandExecutor, CommandExecutor>();

builder.Services.AddSingleton<IServerStatusService, ServerStatusService>();
builder.Services.AddSingleton<IServiceControlService, ServiceControlService>();

builder.Services.AddSingleton<IDownloaderInputParser, DownloaderInputParser>();
builder.Services.AddSingleton<IUserStateService, InMemoryUserStateService>();

builder.Services.AddSingleton<IDockerService, DockerService>();
builder.Services.AddSingleton<ISystemPowerService, SystemPowerService>();

builder.Services.AddSingleton<CommandRouter>();

builder.Services.AddHostedService<DobbyBotWorker>();

var host = builder.Build();

host.Run();