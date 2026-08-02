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

builder.Logging.AddFilter(
    "System.Net.Http",
    LogLevel.None);

builder.Logging.AddFilter(
    "System.Net.Http.HttpClient",
    LogLevel.None);

builder.Services
    .AddOptions<DobbyBotOptions>()
    .Bind(
        builder.Configuration.GetSection(
            DobbyBotOptions.SectionName))
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.Token),
        "DobbyBot__Token is missing.")
    .Validate(
        options =>
            options.AdminTelegramId > 0,
        "DobbyBot__AdminTelegramId is missing or invalid.")
    .ValidateOnStart();

builder.Services.AddHttpClient<TelegramGateway>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(40);
});

builder.Services.AddSingleton<IAdminGuard, AdminGuard>();
builder.Services.AddSingleton<ICommandExecutor, CommandExecutor>();

builder.Services.AddSingleton<
    IServerStatusService,
    ServerStatusService>();

builder.Services.AddSingleton<
    IServiceControlService,
    ServiceControlService>();

builder.Services.AddSingleton<
    IDownloaderInputParser,
    DownloaderInputParser>();

builder.Services.AddSingleton<
    IUserStateService,
    InMemoryUserStateService>();

builder.Services.AddSingleton<
    IDockerService,
    DockerService>();

builder.Services.AddSingleton<
    ISystemPowerService,
    SystemPowerService>();

builder.Services.AddSingleton<CommandRouter>();

builder.Services.AddHostedService<DobbyBotWorker>();

var host = builder.Build();

host.Run();