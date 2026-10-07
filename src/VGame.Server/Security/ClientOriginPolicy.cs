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
        return remoteAddress is not null && IPAddress.IsLoopback(remoteAddress);
    }
}