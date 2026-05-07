extern alias AppHostProject;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenIdentityStack.Contract.Tests.Fixtures;
using OpenIdentityStack.Testing;

[assembly: AssemblyFixture(typeof(AppHostFixture))]

namespace OpenIdentityStack.Contract.Tests.Fixtures;

/// <summary>
/// Shared fixture for Aspire-based integration tests.
/// Uses internal test seeding without API endpoints.
/// </summary>
public class AppHostFixture : IAsyncLifetime
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    
    public IDistributedApplicationTestingBuilder? Builder { get; private set; }
    public DistributedApplication? App { get; private set; }
    public HttpClient? HttpClient { get; private set; }
    private OpenIdentityStackTestSeeder? TestSeeder { get; set; }

    public async ValueTask InitializeAsync()
    {
        // Reference the AppHost project type to ensure it's loaded
        _ = typeof(AppHostProject::Projects.OpenIdentityStack_AppHost);

        this.Builder = await AspireTestApplication.CreateBuilderAsync<AppHostProject::Projects.OpenIdentityStack_AppHost>();

        this.App = await this.Builder.BuildAsync();

        await this.App.StartAsync();

        // Wait for the API resource to be healthy
        using var cts = new CancellationTokenSource(DefaultTimeout);
        await this.App.ResourceNotifications.WaitForResourceHealthyAsync("postgres", cts.Token);
        await this.App.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token);

        this.HttpClient = this.App.CreateHttpClient("api");
        this.HttpClient.Timeout = RequestTimeout;

        string connectionString = await GetRequiredConnectionStringAsync("openidentitystack");
        this.TestSeeder = await OpenIdentityStackTestSeeder.CreateAsync(connectionString);
    }

    /// <summary>
    /// Creates a test service account using the test seeding API endpoint.
    /// This is the proper closed-box testing approach - all seeding goes through the API.
    /// </summary>
    /// <param name="clientId">The client ID for the service account.</param>
    /// <param name="clientSecret">The client secret for the service account.</param>
    public async Task CreateServiceAccountAsync(
        string clientId,
        string clientSecret,
        IReadOnlyList<string>? allowedScopes = null)
    {
        if (this.TestSeeder is null)
        {
            throw new InvalidOperationException("TestSeeder is not initialized.");
        }

        await this.TestSeeder.CreateServiceAccountAsync(clientId, clientSecret, allowedScopes);
    }

    /// <summary>
    /// Obtains an access token for a service account using client credentials flow.
    /// </summary>
    /// <param name="clientId">The client ID.</param>
    /// <param name="clientSecret">The client secret.</param>
    /// <returns>The access token.</returns>
    public async Task<string> GetAccessTokenAsync(
        string clientId,
        string clientSecret,
        string scope = "api")
    {
        if (this.HttpClient is null)
        {
            throw new InvalidOperationException("HttpClient is not initialized.");
        }

        HttpResponseMessage response = await this.HttpClient.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = scope
        }));

        if (!response.IsSuccessStatusCode)
        {
            string content = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to obtain access token: {response.StatusCode} - {content}");
        }

        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        return json?["access_token"]?.GetValue<string>() ?? throw new InvalidOperationException("Access token not found in response.");
    }

    /// <summary>
    /// Creates an authenticated HttpClient with a bearer token.
    /// </summary>
    /// <param name="clientId">The client ID for authentication.</param>
    /// <param name="clientSecret">The client secret for authentication.</param>
    /// <returns>An HttpClient configured with the bearer token.</returns>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        string clientId,
        string clientSecret,
        string scope = "api",
        IReadOnlyList<string>? allowedScopes = null)
    {
        await this.CreateServiceAccountAsync(clientId, clientSecret, allowedScopes);
        string token = await this.GetAccessTokenAsync(clientId, clientSecret, scope);
        
        if (this.App is null)
        {
            throw new InvalidOperationException("App is not initialized.");
        }

        HttpClient client = this.App.CreateHttpClient("api");
        client.Timeout = RequestTimeout;
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<Guid> CreateSessionAsync(
        Guid userId,
        string? ipAddress = null,
        string? userAgent = null,
        int? durationMinutes = null)
    {
        if (this.TestSeeder is null)
        {
            throw new InvalidOperationException("TestSeeder is not initialized.");
        }

        return await this.TestSeeder.CreateSessionAsync(userId, ipAddress, userAgent, durationMinutes);
    }

    public async Task ValidateUserCredentialsAsync(string email, string password)
    {
        if (this.TestSeeder is null)
        {
            throw new InvalidOperationException("TestSeeder is not initialized.");
        }

        await this.TestSeeder.ValidateUserCredentialsAsync(email, password);
    }

    /// <summary>
    /// Creates a test user via the test seeding API.
    /// </summary>
    public async Task<Guid> CreateTestUserAsync(string email, string displayName, string password)
    {
        if (this.TestSeeder is null)
        {
            throw new InvalidOperationException("TestSeeder is not initialized.");
        }

        return await this.TestSeeder.CreateTestUserAsync(email, displayName, password);
    }

    /// <summary>
    /// Creates a test role via the test seeding API.
    /// </summary>
    public async Task<Guid> CreateTestRoleAsync(string name, string? description = null)
    {
        if (this.TestSeeder is null)
        {
            throw new InvalidOperationException("TestSeeder is not initialized.");
        }

        return await this.TestSeeder.CreateTestRoleAsync(name, description);
    }

    /// <summary>
    /// Assigns a role to a user via the test seeding API.
    /// </summary>
    public async Task AssignRoleToUserAsync(Guid userId, Guid roleId)
    {
        if (this.TestSeeder is null)
        {
            throw new InvalidOperationException("TestSeeder is not initialized.");
        }

        await this.TestSeeder.AssignRoleToUserAsync(userId, roleId);
    }

    /// <summary>
    /// Verifies/activates a user via the test seeding API.
    /// </summary>
    public async Task VerifyUserAsync(Guid userId)
    {
        if (this.TestSeeder is null)
        {
            throw new InvalidOperationException("TestSeeder is not initialized.");
        }

        await this.TestSeeder.VerifyUserAsync(userId);
    }

    public async ValueTask DisposeAsync()
    {
        HttpClient?.Dispose();
        if (TestSeeder is not null)
        {
            await TestSeeder.DisposeAsync();
        }
        if (this.App is not null)
        {
            await this.App.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }

    private async Task<string> GetRequiredConnectionStringAsync(string name)
    {
        if (App is null)
        {
            throw new InvalidOperationException("App is not initialized.");
        }

        string? connectionString = await App.GetConnectionStringAsync(name);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{name}' was not found.");
        }

        return connectionString;
    }
}
