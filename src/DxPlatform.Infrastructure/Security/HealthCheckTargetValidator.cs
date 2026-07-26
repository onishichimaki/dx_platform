using System.Net;
using System.Net.Sockets;
using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Validation;
using DxPlatform.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace DxPlatform.Infrastructure.Security;

public sealed class SystemDnsResolver : IDnsResolver
{
    public Task<IPAddress[]> GetHostAddressesAsync(
        string host,
        CancellationToken cancellationToken = default) =>
        Dns.GetHostAddressesAsync(host, cancellationToken);
}

public sealed class HealthCheckTargetValidator(
    IUrlSafetyValidator urlSafetyValidator,
    IDnsResolver dnsResolver,
    IOptions<HealthCheckOptions> options) : IHealthCheckTargetValidator
{
    private static readonly HashSet<IPAddress> KnownMetadataAddresses =
    [
        IPAddress.Parse("100.100.100.200"),
        IPAddress.Parse("168.63.129.16"),
        IPAddress.Parse("fd00:ec2::254")
    ];

    public async Task<bool> IsAllowedAsync(
        string? url,
        CancellationToken cancellationToken = default)
    {
        if (!urlSafetyValidator.IsSafeHttpUrl(url, allowEmpty: false) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var port = uri.IsDefaultPort
            ? uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80
            : uri.Port;
        var addresses = await GetAllowedAddressesAsync(
            uri.DnsSafeHost,
            port,
            cancellationToken);
        return addresses.Count > 0;
    }

    public async Task<IReadOnlyList<IPAddress>> GetAllowedAddressesAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var normalizedHost = host.Trim().TrimEnd('.');
        if (normalizedHost.Length == 0 ||
            !settings.AllowedPorts.Contains(port) ||
            !settings.AllowedHosts.Any(rule => HostMatches(normalizedHost, rule)))
        {
            return [];
        }

        try
        {
            var addresses = await dnsResolver.GetHostAddressesAsync(
                normalizedHost,
                cancellationToken);
            return addresses
                .Where(address => IsAddressAllowed(address, settings.AllowPrivateAddresses))
                .Distinct()
                .ToArray();
        }
        catch (SocketException)
        {
            return [];
        }
    }

    private static bool HostMatches(string host, string configuredRule)
    {
        var rule = configuredRule.Trim().TrimEnd('.');
        if (rule.Length == 0 || rule == "*")
        {
            return false;
        }

        if (rule.StartsWith("*.", StringComparison.Ordinal))
        {
            var suffix = rule[2..];
            return suffix.Length > 0 &&
                   host.EndsWith($".{suffix}", StringComparison.OrdinalIgnoreCase);
        }

        return host.Equals(rule, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsAddressAllowed(IPAddress address, bool allowPrivateAddresses)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None) ||
            address.Equals(IPAddress.IPv6None) ||
            KnownMetadataAddresses.Contains(address))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var first = bytes[0];
            var second = bytes[1];
            var alwaysBlocked = first == 0 || first == 127 ||
                                (first == 169 && second == 254) ||
                                (first == 198 && (second == 18 || second == 19)) ||
                                first >= 224;
            if (alwaysBlocked)
            {
                return false;
            }

            var isPrivate = first == 10 ||
                            (first == 172 && second is >= 16 and <= 31) ||
                            (first == 192 && second == 168) ||
                            (first == 100 && second is >= 64 and <= 127);
            return allowPrivateAddresses || !isPrivate;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal)
            {
                return false;
            }

            var isUniqueLocal = (bytes[0] & 0xfe) == 0xfc;
            return allowPrivateAddresses || !isUniqueLocal;
        }

        return false;
    }
}
