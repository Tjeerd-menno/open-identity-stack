using System.Net;
using System.Net.Sockets;

namespace OpenIdentityStack.Infrastructure.ApplicationPermissions;

/// <summary>
/// Pins each manifest connection to an approved DNS answer so a later lookup cannot
/// change the address between validation and the TCP connection.
/// </summary>
internal static class ManifestDestinationPolicy
{
    internal static bool AllowsLocalTestFixturesForEnvironment(string environmentName) =>
        string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase)
        || string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);

    internal static bool IsSupportedLoopbackFixtureHost(string host)
    {
        string normalizedHost = host.Trim('[', ']');
        return string.Equals(normalizedHost, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedHost, "127.0.0.1", StringComparison.Ordinal)
            || string.Equals(normalizedHost, "::1", StringComparison.Ordinal);
    }

    internal static SocketsHttpHandler CreateHandler(bool allowLocalTestFixtures = false)
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            ConnectCallback = (context, cancellationToken) => ConnectAsync(context.DnsEndPoint, allowLocalTestFixtures, cancellationToken),
        };
    }

    internal static async ValueTask<Stream> ConnectAsync(
        DnsEndPoint endpoint,
        bool allowLocalTestFixtures,
        CancellationToken cancellationToken)
    {
        IPAddress[] addresses = await ResolveApprovedAddressesAsync(
            endpoint.Host,
            static (host, token) => Dns.GetHostAddressesAsync(host, token),
            allowLocalTestFixtures && IsSupportedLoopbackFixtureHost(endpoint.Host),
            cancellationToken).ConfigureAwait(false);

        Exception? lastError = null;
        foreach (IPAddress address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (exception is SocketException or IOException)
            {
                lastError = exception;
                socket.Dispose();
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new HttpRequestException("The permissions manifest endpoint could not be reached.", lastError);
    }

    internal static async Task<IPAddress[]> ResolveApprovedAddressesAsync(
        string host,
        Func<string, CancellationToken, Task<IPAddress[]>> resolve,
        bool allowLocalTestFixtures,
        CancellationToken cancellationToken)
    {
        IPAddress[] addresses = await resolve(host, cancellationToken).ConfigureAwait(false);
        if (addresses.Length == 0 || addresses.Any(address => !IsApprovedAddress(address, allowLocalTestFixtures)))
        {
            throw new HttpRequestException("The permissions manifest endpoint address is not approved.");
        }

        return addresses;
    }

    internal static bool IsApprovedAddress(IPAddress address, bool allowLocalTestFixtures = false)
    {
        if (allowLocalTestFixtures && IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        byte[] bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            byte first = bytes[0];
            byte second = bytes[1];
            byte third = bytes[2];
            return first is >= 1 and <= 223
                && first != 10
                && first != 127
                && !(first == 100 && second is >= 64 and <= 127)
                && !(first == 169 && second == 254)
                && !(first == 172 && second is >= 16 and <= 31)
                && !(first == 192 && second == 0 && third == 0)
                && !(first == 192 && second == 0 && third == 2)
                && !(first == 192 && second == 88 && third == 99)
                && !(first == 192 && second == 168)
                && !(first == 198 && second is 18 or 19)
                && !(first == 198 && second == 51 && third == 100)
                && !(first == 203 && second == 0 && third == 113);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return (bytes[0] & 0xe0) == 0x20 // Globally routed 2000::/3 only.
                && !(bytes[0] == 0x3f && (bytes[1] & 0xf0) == 0xf0) // Documentation.
                && !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8) // Documentation.
                && !(bytes[0] == 0x20 && bytes[1] == 0x02) // 6to4 embeds an IPv4 destination.
                && !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] <= 0x01); // Special-purpose /23.
        }

        return false;
    }
}
