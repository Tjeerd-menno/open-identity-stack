using System.Net;
using System.Text;
using OpenIdentityStack.Application.ApplicationPermissions.Validators;
using OpenIdentityStack.Infrastructure.ApplicationPermissions;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.ApplicationPermissions;

public sealed class RemotePermissionManifestFetcherTests
{
    [Fact]
    public async Task FetchAsync_WithHttpsBaseUrl_FetchesWellKnownManifest()
    {
        StubHttpMessageHandler handler = new(_ => JsonResponse(Manifest("orders-api")));
        RemotePermissionManifestFetcher fetcher = new(new HttpClient(handler));

        Result<PermissionManifestDocument> result = await fetcher.FetchAsync("https://orders.example.com/api", "orders-api");

        result.IsSuccess.ShouldBeTrue();
        handler.Requests.Single().RequestUri!.ToString().ShouldBe("https://orders.example.com/api/.well-known/permissions");
        result.Value.Application.Id.ShouldBe("orders-api");
    }

    [Fact]
    public async Task FetchAsync_WithWrongApplicationId_ReturnsValidationError()
    {
        RemotePermissionManifestFetcher fetcher = new(new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(Manifest("billing-api")))));

        Result<PermissionManifestDocument> result = await fetcher.FetchAsync("https://orders.example.com", "orders-api");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionManifest.ApplicationIdMismatch");
    }

    [Theory]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    public async Task FetchAsync_WithRedirectResponse_ReturnsValidationError(HttpStatusCode statusCode)
    {
        RemotePermissionManifestFetcher fetcher = new(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode))));

        Result<PermissionManifestDocument> result = await fetcher.FetchAsync("https://orders.example.com", "orders-api");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionManifest.RemoteRedirectRejected");
    }

    [Fact]
    public async Task FetchAsync_WithUnsupportedContentType_ReturnsValidationError()
    {
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(Manifest("orders-api"), Encoding.UTF8, "text/plain"),
        };
        RemotePermissionManifestFetcher fetcher = new(new HttpClient(new StubHttpMessageHandler(_ => response)));

        Result<PermissionManifestDocument> result = await fetcher.FetchAsync("https://orders.example.com", "orders-api");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionManifest.RemoteContentTypeUnsupported");
    }

    [Fact]
    public async Task FetchAsync_WithOversizedContentLength_ReturnsValidationError()
    {
        HttpResponseMessage response = JsonResponse(Manifest("orders-api"));
        response.Content.Headers.ContentLength = RemotePermissionManifestFetcher.MaxManifestBytes + 1;
        RemotePermissionManifestFetcher fetcher = new(new HttpClient(new StubHttpMessageHandler(_ => response)));

        Result<PermissionManifestDocument> result = await fetcher.FetchAsync("https://orders.example.com", "orders-api");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionManifest.RemoteResponseTooLarge");
    }

    [Fact]
    public async Task FetchAsync_WhenRequestTimesOut_ReturnsValidationError()
    {
        RemotePermissionManifestFetcher fetcher = new(new HttpClient(new StubHttpMessageHandler(_ => throw new TaskCanceledException("timeout"))));

        Result<PermissionManifestDocument> result = await fetcher.FetchAsync("https://orders.example.com", "orders-api");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionManifest.RemoteFetchTimedOut");
    }

    [Fact]
    public async Task FetchAsync_WithPlainHttpNonLocalhostBaseUrl_ReturnsValidationError()
    {
        RemotePermissionManifestFetcher fetcher = new(new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(Manifest("orders-api")))));

        Result<PermissionManifestDocument> result = await fetcher.FetchAsync("http://orders.example.com", "orders-api");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionManifest.RemoteBaseUrlUntrusted");
    }

    [Fact]
    public async Task FetchAsync_WithPlainHttpLocalhostBaseUrl_IsAllowedForDevelopmentFixtures()
    {
        RemotePermissionManifestFetcher fetcher = new(new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(Manifest("orders-api")))));

        Result<PermissionManifestDocument> result = await fetcher.FetchAsync("http://localhost:5010/fixtures", "orders-api");

        result.IsSuccess.ShouldBeTrue();
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static string Manifest(string applicationId)
    {
        return $$"""
            {
              "schemaVersion": "1.0.0",
              "application": {
                "id": "{{applicationId}}",
                "displayName": "Orders API",
                "description": null,
                "version": "2.0.0"
              },
              "permissions": [
                {
                  "key": "order:read",
                  "displayName": "Read orders",
                  "description": null,
                  "category": "Orders"
                }
              ]
            }
            """;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }
}
