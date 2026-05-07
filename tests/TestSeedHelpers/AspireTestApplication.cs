using System.Net;
using Aspire.Hosting.Testing;

namespace OpenIdentityStack.Testing;

public static class AspireTestApplication
{
    private static readonly string[] TestingArgs =
    [
        "--DcpPublisher:RandomizePorts=true"
    ];

    public static Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync<TEntryPoint>(
        CancellationToken cancellationToken = default)
        where TEntryPoint : class
    {
        Environment.SetEnvironmentVariable("OPENIDENTITYSTACK_DISABLE_DATA_VOLUME", "true");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        return DistributedApplicationTestingBuilder.CreateAsync<TEntryPoint>(TestingArgs, cancellationToken);
    }

    public static async Task WaitForHttpReadyAsync(
        HttpClient client,
        TimeSpan timeout,
        string path = "/",
        CancellationToken cancellationToken = default)
    {
        DateTime startedAt = DateTime.UtcNow;
        HttpStatusCode? lastStatusCode = null;

        while (DateTime.UtcNow - startedAt < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using HttpResponseMessage response = await client.GetAsync(path, cancellationToken);
                lastStatusCode = response.StatusCode;

                if ((int)response.StatusCode < 500 && response.StatusCode != HttpStatusCode.NotFound)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        string lastObservation = lastStatusCode is { } statusCode
            ? $" Last status code: {(int)statusCode} ({statusCode})."
            : " No HTTP response was received.";

        throw new TimeoutException($"Timed out waiting for {client.BaseAddress}{path} to become ready after {timeout}.{lastObservation}");
    }
}
