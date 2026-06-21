using DotNetEnv;
using DobbyBot.Worker.Bot;
using DobbyBot.Worker.Commands;
using DobbyBot.Worker.Execution;
using DobbyBot.Worker.Options;
using DobbyBot.Worker.Security;
using DobbyBot.Worker.Services;
using Microsoft.Extensions.DependencyInjection;
using DobbyBot.Worker.Modules.Downloader;
using DobbyBot.Worker.State;

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


builder.Services.AddSingleton<CommandRouter>();

builder.Services.AddHostedService<DobbyBotWorker>();

var host = builder.Build();

host.Run();