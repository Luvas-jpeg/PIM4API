using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace EquipamentosMedicosApi.Services;

public static class ReverseProxyConfiguration
{
    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = Math.Clamp(configuration.GetValue<int?>("ReverseProxy:ForwardLimit") ?? 1, 1, 5);
        foreach (var value in configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
            options.KnownProxies.Add(IPAddress.Parse(value));
        foreach (var value in configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [])
        {
            var network = System.Net.IPNetwork.Parse(value);
            if (network.PrefixLength == 0) throw new InvalidOperationException("Nao confie em todas as redes para headers de proxy.");
            options.KnownIPNetworks.Add(network);
        }
    }
}
