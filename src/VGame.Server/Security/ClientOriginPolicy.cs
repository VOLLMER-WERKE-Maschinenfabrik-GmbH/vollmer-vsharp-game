using System.Net;

namespace VGame.Server.Security;

public interface IClientOriginPolicy
{
    bool IsLocal(HttpContext? httpContext);
}

public sealed class ClientOriginPolicy : IClientOriginPolicy
{
    public bool IsLocal(HttpContext? httpContext)
    {
        var remoteAddress = httpContext?.Connection.RemoteIpAddress;
        if (httpContext is null || remoteAddress is null || !IPAddress.IsLoopback(remoteAddress)) return false;
        var request = httpContext.Request;
        var host = request.Host.Host;
        if (!(host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address))) return false;
        if (request.Headers["Sec-Fetch-Site"] == "cross-site") return false;
        var origin = request.Headers.Origin.ToString();
        return origin.Length == 0 || Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
            && originUri.GetLeftPart(UriPartial.Authority).Equals($"{request.Scheme}://{request.Host}", StringComparison.OrdinalIgnoreCase);
    }
}