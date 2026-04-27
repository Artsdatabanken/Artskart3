using System.Net;

namespace Artskart3.Api.Middleware;

public class ClientSafeListMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ClientSafeListMiddleware> _logger;
    private readonly byte[][] _safeList;

    public ClientSafeListMiddleware(RequestDelegate next, ILogger<ClientSafeListMiddleware> logger, string safeList)
    {
        var ips = safeList.Split(';');
        _safeList = new byte[ips.Length][];

        for (var i = 0; i < ips.Length; i++)
        {
            _safeList[i] = IPAddress.Parse(ips[i]).GetAddressBytes();
        }

        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        _logger.LogDebug("Request from remote IP adress: {RemoteIp}", remoteIp);
        var bytes = remoteIp.GetAddressBytes();
        var badIp = true;
        foreach (var address in _safeList)
        {
            if (address.SequenceEqual(bytes))
            {
                badIp = false;
                break;
            }
        }

        if (badIp)
        {
            _logger.LogWarning("Forbidden Request from Remote IP address: {RemoteIp}", remoteIp);
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return;
        }

        await _next.Invoke(context);
    }
}