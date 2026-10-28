#if DEBUG
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using VGame.Contracts;
using VGame.Server.Security;
using VGame.Tools.Manifest;

namespace VGame.Server.Services;

public static class ZoneEditorEndpoints
{
    private static readonly JsonSerializerOptions PreviewJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new OutcomeJsonConverter(), new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static bool IsAllowed(HttpContext context, IWebHostEnvironment environment, IClientOriginPolicy policy)
    {
        var host = context.Request.Host.Host;
        if (!environment.IsDevelopment() || !policy.IsLocal(context)
            || !(host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address))) return false;
        if (context.Request.Headers["Sec-Fetch-Site"] == "cross-site") return false;
        var origin = context.Request.Headers.Origin.ToString();
        return origin.Length == 0 || Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
            && originUri.GetLeftPart(UriPartial.Authority).Equals($"{context.Request.Scheme}://{context.Request.Host}", StringComparison.OrdinalIgnoreCase);
    }

    public static void Map(WebApplication app)
    {
        app.MapGet("/tools/zones", (IWebHostEnvironment environment, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.File(Path.Combine(environment.WebRootPath, "client/dist/zones.html"), "text/html; charset=utf-8");
        });
        var group = app.MapGroup("/tools/zones/api");
        group.MapGet("/assets", (ZoneEditorService service) => Results.Ok(service.ListAssets()));
        group.MapGet("/asset/{assetId}", (string assetId, ZoneEditorService service) => Execute(() => Results.Ok(service.Read(assetId))));
        group.MapPost("/validate/{assetId}", (string assetId, JsonElement document, ZoneEditorService service) => Execute(() =>
        {
            service.Validate(assetId, document);
            return Results.Ok(new { valid = true });
        }));
        group.MapPost("/asset/{assetId}", (string assetId, JsonElement document, ZoneEditorService service, HttpContext context) => Execute(() =>
            Results.Ok(new { version = service.Save(assetId, document, context.Request.Headers.IfMatch.ToString()) })));
        group.MapPost("/preview", (ZonePreviewRequest request, ZoneEditorService service) => Execute(() => Results.Json(service.Preview(request), PreviewJsonOptions)));
        group.MapGet("/preview", (string assetId, string mode, [FromQuery(Name = "x")] double horizontalPosition,
            [FromQuery(Name = "y")] double verticalPosition, [FromQuery(Name = "endX")] double? horizontalEnd,
            [FromQuery(Name = "endY")] double? verticalEnd, ZoneEditorService service) => Execute(() =>
        {
            if (mode == "swipe" && (horizontalEnd is null || verticalEnd is null)) throw new ArgumentException("Endpunkt der Testgeste fehlt.");
            IReadOnlyList<Point2> points = mode == "swipe" ? [new(horizontalPosition, verticalPosition), new(horizontalEnd!.Value, verticalEnd!.Value)]
                : [new(horizontalPosition, verticalPosition)];
            return Results.Json(service.Preview(new(service.Read(assetId).Zones, mode, points)), PreviewJsonOptions);
        }));
    }

    private static IResult Execute(Func<IResult> action)
    {
        try { return action(); }
        catch (FileNotFoundException) { return Results.NotFound(new { error = "Werkzeugbild oder Zonendatei nicht verfügbar." }); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { return Results.Problem(statusCode: 503, title: "Die Zonendatei kann derzeit nicht gelesen oder gespeichert werden."); }
        catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
        catch (Exception exception) when (exception is ArgumentException or JsonException or ManifestValidationException or NetTopologySuite.Geometries.TopologyException)
        { return Results.BadRequest(new { valid = false, error = "Ungültige Zonen oder Testgeste. Abdeckung, Geometrie und Koordinaten müssen gültig sein." }); }
    }
}
#endif