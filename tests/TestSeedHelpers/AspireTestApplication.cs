using System.Net;
using Aspire.Hosting.Testing;

namespace OpenIdentityStack.Testing;

public static class AspireTestApplication
{
    private static readonly string[] TestingArgs =
    [
        "--DcpPublisher:RandomizePorts=true",
        "--DcpPublisher:DependencyCheckTimeout=120",
        "--DcpPublisher:ContainerRuntimeInitializationTimeout=00:02:00"
    ];

    public static Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync<TEntryPoint>(
        bool includeAdminWeb = false,
        CancellationToken cancellationToken = default)
        where TEntryPoint : class
    {
        Environment.SetEnvironmentVariable("OPENIDENTITYSTACK_DISABLE_DATA_VOLUME", "true");
        Environment.SetEnvironmentVariable("OPENIDENTITYSTACK_ENABLE_ADMINWEB", includeAdminWeb ? "true" : "false");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("Parameters__default-admin-password", "Test1234@Test1234");
        Environment.SetEnvironmentVariable("DEBUG_SESSION_PORT", null);
        Environment.SetEnvironmentVariable("DEBUG_SESSION_TOKEN", null);

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
