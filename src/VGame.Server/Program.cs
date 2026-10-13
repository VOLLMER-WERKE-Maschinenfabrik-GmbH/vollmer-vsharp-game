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
using Microsoft.Extensions.FileProviders;

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
var workshopEventOptions = builder.Configuration.GetSection("WorkshopEvents")
    .Get<WorkshopEventOptions>(options => options.ErrorOnUnknownConfiguration = true) ?? new();
workshopEventOptions.Validate();
var artworkCatalogPath = Path.Combine(Path.GetDirectoryName(gameplayConfigurationPath)!, "artwork.prototype.json");
var artworkJsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
artworkJsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
IReadOnlyList<ToolDefinition> artworkTools = File.Exists(artworkCatalogPath)
    ? JsonSerializer.Deserialize<ToolDefinition[]>(File.ReadAllText(artworkCatalogPath), artworkJsonOptions)
        ?? throw new InvalidDataException("Der Artwork-Prototypkatalog ist leer.")
    : [];
builder.Services.AddSingleton(artworkTools);
#if DEBUG
builder.Services.AddSingleton(new ZoneEditorService(Path.GetDirectoryName(Path.GetDirectoryName(gameplayConfigurationPath))!,
    Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(gameplayConfigurationPath)))!, "media-source"), artworkTools, gameplayOptions));
#endif
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<GameEngine>();
builder.Services.AddSingleton<IClientOriginPolicy, ClientOriginPolicy>();
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new OutcomeJsonConverter());
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });
builder.Services.AddHostedService<RoundClockService>();
builder.Services.AddHostedService<PrototypeToolSpawnerService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var editorResource = path.StartsWithSegments("/tools/zones") || path == "/client/dist/zones.html"
        || path.StartsWithSegments("/client/dist/assets") && path.Value?.Contains("/zones-", StringComparison.Ordinal) == true;
    if (editorResource)
    {
        var allowed = false;
#if DEBUG
        allowed = ZoneEditorEndpoints.IsAllowed(context, app.Environment, context.RequestServices.GetRequiredService<IClientOriginPolicy>());
#endif
        if (!allowed)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        context.Response.Headers.CacheControl = "no-store";
    }
    await next(context);
});

app.UseStaticFiles();
var assetDirectory = Path.GetDirectoryName(Path.GetDirectoryName(gameplayConfigurationPath))!;
if (Directory.Exists(Path.Combine(assetDirectory, "images")))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Path.Combine(assetDirectory, "images")),
        RequestPath = "/Assets/images",
        ServeUnknownFileTypes = false
    });
}
if (Directory.Exists(Path.Combine(assetDirectory, "audio")))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Path.Combine(assetDirectory, "audio")),
        RequestPath = "/Assets/audio",
        ServeUnknownFileTypes = false
    });
}
app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new { status = "ready" }));
app.MapGet("/api/game-audio", (HttpContext context, ILogger<Program> logger) =>
{
    context.Response.Headers.CacheControl = "no-store";
    try
    {
        var manifest = JsonSerializer.Deserialize<AssetManifest>(File.ReadAllText(Path.Combine(assetDirectory, "config/manifest.json")), artworkJsonOptions)
            ?? throw new InvalidDataException("Audio manifest is empty.");
        string[] requiredPools = ["laser_success", "laser_fail", "laser_effect", "saw_success", "saw_fail", "grind_effect"];
        var pools = manifest.AudioPools;
        if (pools is null || pools.Count != requiredPools.Length || pools.Any(pool => pool is null)
            || pools.Select(pool => pool.PoolId).Distinct(StringComparer.Ordinal).Count() != requiredPools.Length
            || pools.Any(pool => !requiredPools.Contains(pool.PoolId, StringComparer.Ordinal) || pool.Variants is null
                || pool.BagRule != "shuffle_no_repeat" || pool.VolumeProfile is not ("voice" or "feedback")
                || pool.Variants.Any(variant => variant is null || !System.Text.RegularExpressions.Regex.IsMatch(variant, "^(laser|grind)/[a-z0-9_]+\\.(wav|mp3|ogg)$")
                    || !File.Exists(Path.Combine(assetDirectory, "audio", variant)))
                || pool.Fallback != "generated_tone" && !requiredPools.Contains(pool.Fallback, StringComparer.Ordinal)))
            throw new InvalidDataException("Audio manifest has invalid pools or file references.");
        foreach (var pool in pools)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { pool.PoolId };
            var fallback = pool.Fallback;
            while (fallback != "generated_tone")
            {
                if (!visited.Add(fallback)) throw new InvalidDataException("Audio fallback cycle.");
                fallback = pools.Single(candidate => candidate.PoolId == fallback).Fallback;
            }
        }
        return Results.Ok(new { pools });
    }
    catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException)
    {
        logger.LogWarning("Audiokonfiguration kann nicht geladen werden: {Message}", exception.Message);
        return Results.Problem(statusCode: 503, title: "Audiokonfiguration ist nicht verfügbar.");
    }
});
app.MapGet("/api/workshop-events", (IConfiguration configuration, HttpContext context, ILogger<WorkshopEventOptions> logger) =>
{
    context.Response.Headers.CacheControl = "no-store";
    try
    {
        var options = configuration.GetSection("WorkshopEvents")
            .Get<WorkshopEventOptions>(binding => binding.ErrorOnUnknownConfiguration = true) ?? new();
        options.Validate();
        return Results.Ok(options);
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
    {
        logger.LogWarning("Die Ereigniskonfiguration ist ungültig: {Message}", exception.Message);
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Die Ereigniskonfiguration ist nicht verfügbar.");
    }
});
app.MapGet("/play", (IWebHostEnvironment environment, HttpContext context) => MapClientPage(environment, context, "play.html"));
app.MapGet("/watcher", (IWebHostEnvironment environment, HttpContext context) => MapClientPage(environment, context, "watcher.html"));
#if DEBUG
if (app.Environment.IsDevelopment()) ZoneEditorEndpoints.Map(app);
#endif
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

static IResult MapClientPage(IWebHostEnvironment environment, HttpContext context, string pageName)
{
    context.Response.Headers.CacheControl = "no-store";
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
