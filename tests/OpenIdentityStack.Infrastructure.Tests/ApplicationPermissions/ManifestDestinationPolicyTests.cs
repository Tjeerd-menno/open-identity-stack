using System.Net;
using System.Net.Sockets;
using System.Text;
using OpenIdentityStack.Infrastructure.ApplicationPermissions;

namespace OpenIdentityStack.Infrastructure.Tests.ApplicationPermissions;

public sealed class ManifestDestinationPolicyTests
{
    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("1.1.1.1", true)]
    [InlineData("2001:4860:4860::8888", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("198.19.0.1", false)]
    [InlineData("192.0.2.1", false)]
    [InlineData("203.0.113.1", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("2002:c0a8:0101::", false)]
    [InlineData("3fff::1", false)]
    public void IsApprovedAddress_OnlyAllowsGloballyRoutableAddresses(string text, bool approved)
    {
        ManifestDestinationPolicy.IsApprovedAddress(IPAddress.Parse(text)).ShouldBe(approved);
    }

    [Fact]
    public async Task ResolveApprovedAddressesAsync_RejectsMixedPublicAndPrivateAnswers()
    {
        Task<IPAddress[]> Resolve(string host, CancellationToken cancellationToken) =>
            Task.FromResult(new[] { IPAddress.Parse("1.1.1.1"), IPAddress.Parse("169.254.169.254") });

        await Should.ThrowAsync<HttpRequestException>(() => ManifestDestinationPolicy.ResolveApprovedAddressesAsync(
            "manifest.example", Resolve, false, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveApprovedAddressesAsync_ChecksEachLookupAfterDnsChanges()
    {
        int calls = 0;
        Task<IPAddress[]> Resolve(string host, CancellationToken cancellationToken)
        {
            calls++;
            return Task.FromResult(new[] { IPAddress.Parse(calls == 1 ? "8.8.8.8" : "127.0.0.1") });
        }

        IPAddress[] first = await ManifestDestinationPolicy.ResolveApprovedAddressesAsync(
            "manifest.example", Resolve, false, CancellationToken.None);
        first.Single().ShouldBe(IPAddress.Parse("8.8.8.8"));

        await Should.ThrowAsync<HttpRequestException>(() => ManifestDestinationPolicy.ResolveApprovedAddressesAsync(
            "manifest.example", Resolve, false, CancellationToken.None));
        calls.ShouldBe(2);
    }

    [Fact]
    public void CreateHandler_DisablesRedirectsAndProxies()
    {
        using SocketsHttpHandler handler = ManifestDestinationPolicy.CreateHandler();

        handler.AllowAutoRedirect.ShouldBeFalse();
        handler.UseProxy.ShouldBeFalse();
        handler.ConnectCallback.ShouldNotBeNull();
    }

    [Fact]
    public async Task StrictHandler_RejectsLoopbackBeforeOpeningSocket()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var client = new HttpClient(ManifestDestinationPolicy.CreateHandler());

        await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync($"http://127.0.0.1:{port}/.well-known/permissions"));

        listener.Pending().ShouldBeFalse();
    }

    [Fact]
    public async Task Handler_ReturnsRedirectWithoutFollowingPrivateLocation()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var server = Task.Run(async () =>
        {
            using TcpClient peer = await listener.AcceptTcpClientAsync(cancellation.Token);
            NetworkStream stream = peer.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellation.Token)))
            {
            }

            byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nLocation: http://169.254.169.254/.well-known/permissions\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(response, cancellation.Token);
            await stream.FlushAsync(cancellation.Token);
        });

        using var client = new HttpClient(ManifestDestinationPolicy.CreateHandler(allowLocalTestFixtures: true));
        using HttpResponseMessage response = await client.GetAsync($"http://localhost:{port}/.well-known/permissions", cancellation.Token);
        await server;

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.Host.ShouldBe("169.254.169.254");
    }

    [Fact]
    public void IsApprovedAddress_TestFixturesOnlyAllowLoopbackByExplicitOptIn()
    {
        ManifestDestinationPolicy.IsApprovedAddress(IPAddress.Loopback, true).ShouldBeTrue();
        ManifestDestinationPolicy.IsApprovedAddress(IPAddress.IPv6Loopback, true).ShouldBeTrue();
        ManifestDestinationPolicy.IsApprovedAddress(IPAddress.Parse("169.254.169.254"), true).ShouldBeFalse();
    }
}
