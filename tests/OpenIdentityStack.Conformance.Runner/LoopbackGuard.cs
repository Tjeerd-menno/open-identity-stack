using System.Net;
using System.Net.Sockets;

namespace OpenIdentityStack.Conformance.Runner;

/// <summary>
/// Decides whether a destination is genuinely on this machine.
/// </summary>
/// <remarks>
/// The runner turns off certificate validation so the local loop can use a
/// self-signed provider certificate. That bypass is only defensible when the
/// destination cannot be anywhere but this machine — off-machine it would let an
/// on-path server present any certificate for the expected hostname, keeping the
/// origin the credential check looks at intact.
///
/// A hostname allowlist is not enough to establish that. The local loop's two
/// hostnames, <c>localhost.emobix.co.uk</c> and <c>*.localtest.me</c>, are
/// *public* DNS names that happen to resolve to 127.0.0.1, so trusting the name
/// means trusting whatever DNS currently answers for it. This resolves the name
/// instead and requires every returned address to be a loopback address.
///
/// A gap remains between this check and the connection that follows it: a DNS
/// answer can change in between. Closing it properly means pinning the local
/// provider's certificate, which is the right move if this ever runs anywhere
/// less disposable than a workstation loop.
/// </remarks>
internal static class LoopbackGuard
{
    public static bool IsLoopback(Uri uri)
    {
        // Covers localhost, 127.0.0.0/8 and ::1 without touching the resolver.
        if (uri.IsLoopback)
        {
            return true;
        }

        try
        {
            IPAddress[] addresses = Dns.GetHostAddresses(uri.DnsSafeHost);

            // All, not Any: a name answering with one loopback and one routable
            // address can still send the browser off-machine.
            return addresses.Length > 0 && Array.TrueForAll(addresses, IPAddress.IsLoopback);
        }
        catch (SocketException)
        {
            // Unresolvable is not loopback.
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static bool IsLoopback(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri) && IsLoopback(uri);
}
