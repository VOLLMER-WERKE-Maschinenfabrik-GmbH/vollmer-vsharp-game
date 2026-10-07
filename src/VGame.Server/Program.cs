using System.Text.Json;
using System.Text.Json.Serialization;
using VGame.Server.Components;
using VGame.Contracts;
using VGame.Domain;
using VGame.Server.Hubs;
using VGame.Server.Security;
using VGame.Server.Services;
using System.ComponentModel;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
var hostUrl = builder.Configuration["Host:Urls"] ?? "http://127.0.0.1:5080";
builder.WebHost.UseUrls(hostUrl);

var gameplayConfigurationPath = Path.Combine(builder.Environment.ContentRootPath, "Assets", "config", "gameplay.json");
if (!File.Exists(gameplayConfigurationPath))
{
    gameplayConfigurationPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "Assets", "config", "gameplay.json"));
}

if (File.Exists(gameplayConfigurationPath))
{
    builder.Configuration.AddJsonFile(gameplayConfigurationPath, optional: false, reloadOnChange: true);
}

var gameplayOptions = builder.Configuration.Get<GameplayOptions>() ?? new GameplayOptions();
gameplayOptions.Validate();
builder.Services.AddSingleton(gameplayOptions);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<GameEngine>();
builder.Services.AddSingleton<IClientOriginPolicy, ClientOriginPolicy>();
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.PayloadSerializerOptions.Converters.Add(new OutcomeJsonConverter());
    });
builder.Services.AddHostedService<RoundClockService>();
builder.Services.AddHostedService<PrototypeToolSpawnerService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new { status = "ready" }));
app.MapGet("/play", (IWebHostEnvironment environment) => MapClientPage(environment, "play.html"));
app.MapGet("/watcher", (IWebHostEnvironment environment) => MapClientPage(environment, "watcher.html"));
app.MapHub<GameHub>("/gameHub");
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

var openPlayer = builder.Configuration.GetValue<bool>("Host:OpenPlayer");
var openWatcher = builder.Configuration.GetValue<bool>("Host:OpenWatcher");
if (openPlayer || openWatcher)
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        if (openPlayer)
        {
            OpenBrowser(hostUrl, "/play", app.Logger);
        }

        if (openWatcher)
        {
            OpenBrowser(hostUrl, "/watcher", app.Logger);
        }
    });
}

app.Run();

static IResult MapClientPage(IWebHostEnvironment environment, string pageName)
{
    var builtPage = Path.Combine(environment.WebRootPath, "client", "dist", pageName);
    var fallbackPage = Path.Combine(environment.WebRootPath, pageName);
    var pagePath = File.Exists(builtPage) ? builtPage : fallbackPage;

    return Results.File(pagePath, "text/html; charset=utf-8");
}

static void OpenBrowser(string hostUrl, string path, ILogger logger)
{
    try
    {
        Process.Start(new ProcessStartInfo(new Uri(new Uri(hostUrl), path).AbsoluteUri)
        {
            UseShellExecute = true
        });
    }
    catch (Win32Exception exception)
    {
        logger.LogWarning("Die Browseransicht {Path} konnte nicht geöffnet werden: {Message}", path, exception.Message);
    }
}

public partial class Program
{
}
