using System.Net;
using System.Text.Json;
using OpenIdentityStack.Application.ApplicationPermissions;
using OpenIdentityStack.Application.ApplicationPermissions.Validators;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.ApplicationPermissions;

public sealed class RemotePermissionManifestFetcher : IRemotePermissionManifestFetcher
{
    public const long MaxManifestBytes = 256 * 1024;
    private static readonly TimeSpan defaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient httpClient;

    public RemotePermissionManifestFetcher(HttpClient httpClient)
    {
        this.httpClient = httpClient;
        if (this.httpClient.Timeout == Timeout.InfiniteTimeSpan || this.httpClient.Timeout > defaultTimeout)
        {
            this.httpClient.Timeout = defaultTimeout;
        }
    }

    public async Task<Result<PermissionManifestDocument>> FetchAsync(
        string manifestBaseUrl,
        string expectedApplicationIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildManifestUri(manifestBaseUrl, out Uri? manifestUri))
        {
            return Error("RemoteBaseUrlInvalid", "Manifest base URL must be an absolute HTTP(S) URL.");
        }

        Uri uri = manifestUri!;
        if (!IsTrustedScheme(uri))
        {
            return Error("RemoteBaseUrlUntrusted", "Remote import requires HTTPS except for localhost development fixtures.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using HttpResponseMessage response = await this.httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (IsRedirect(response.StatusCode))
            {
                return Error("RemoteRedirectRejected", "Remote manifest redirects are not allowed.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return Error("RemoteFetchFailed", "Remote manifest endpoint returned a non-success response.");
            }

            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!IsSupportedContentType(mediaType))
            {
                return Error("RemoteContentTypeUnsupported", "Remote manifest endpoint must return application/json or application/permission-manifest+json.");
            }

            if (response.Content.Headers.ContentLength > MaxManifestBytes)
            {
                return Error("RemoteResponseTooLarge", "Remote manifest response exceeded the maximum size.");
            }

            await using Stream boundedStream = await ReadBoundedAsync(response.Content, cancellationToken).ConfigureAwait(false);
            PermissionManifestDocument? manifest = await JsonSerializer
                .DeserializeAsync<PermissionManifestDocument>(boundedStream, serializerOptions, cancellationToken)
                .ConfigureAwait(false);
            if (manifest is null)
            {
                return Error("RemoteInvalidJson", "Remote manifest endpoint did not return a valid manifest.");
            }

            if (!string.Equals(manifest.Application.Id, expectedApplicationIdentifier, StringComparison.Ordinal))
            {
                return Error("ApplicationIdMismatch", "Remote manifest application id must match the registered application.");
            }

            return manifest;
        }
        catch (RemoteManifestTooLargeException)
        {
            return Error("RemoteResponseTooLarge", "Remote manifest response exceeded the maximum size.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Error("RemoteFetchTimedOut", "Remote manifest fetch timed out.");
        }
        catch (JsonException)
        {
            return Error("RemoteInvalidJson", "Remote manifest endpoint did not return a valid manifest.");
        }
        catch (HttpRequestException)
        {
            return Error("RemoteFetchFailed", "Remote manifest endpoint could not be fetched.");
        }
    }

    private static async Task<Stream> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using Stream source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var destination = new MemoryStream();
        byte[] buffer = new byte[8192];
        long total = 0;

        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                destination.Position = 0;
                return destination;
            }

            total += read;
            if (total > MaxManifestBytes)
            {
                throw new RemoteManifestTooLargeException();
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool TryBuildManifestUri(string manifestBaseUrl, out Uri? manifestUri)
    {
        manifestUri = null;
        if (!Uri.TryCreate(manifestBaseUrl, UriKind.Absolute, out Uri? baseUri))
        {
            return false;
        }

        if (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            return false;
        }

        manifestUri = new Uri($"{baseUri.GetLeftPart(UriPartial.Path).TrimEnd('/')}/.well-known/permissions");
        return true;
    }

    private static bool IsTrustedScheme(Uri uri)
    {
        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return true;
        }

        return uri.Scheme == Uri.UriSchemeHttp
            && (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal)
                || string.Equals(uri.Host, "::1", StringComparison.Ordinal));
    }

    private static bool IsSupportedContentType(string? mediaType)
    {
        return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mediaType, "application/permission-manifest+json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
    {
        int code = (int)statusCode;
        return code is >= 300 and <= 399;
    }

    private static DomainError Error(string code, string description)
    {
        return DomainError.Validation($"PermissionManifest.{code}", description);
    }

    private sealed class RemoteManifestTooLargeException : Exception;
}
