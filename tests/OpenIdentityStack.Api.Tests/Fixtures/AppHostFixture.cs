using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenIdentityStack.Infrastructure.Persistence;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenIdentityStack.Api.Tests.Fixtures;
using OpenIdentityStack.Testing;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

[assembly: AssemblyFixture(typeof(AppHostFixture))]
[assembly: Parallelization(Mode = ParallelMode.None)]

namespace OpenIdentityStack.Api.Tests.Fixtures;

/// <summary>
/// Shared fixture for in-process API integration tests.
/// Uses WebApplicationFactory with a prefilled SQLite database.
/// </summary>
public class AppHostFixture : IAsyncLifetime
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private const string ConnectionString = "Data Source=file:openidentitystack_api_tests;Mode=Memory;Cache=Shared";

    private SqliteConnection? Connection { get; set; }
    private OpenIdentityStackApiFactory? Factory { get; set; }
    private string? PreviousConnectionString { get; set; }
    private string? PreviousOpenIddictIssuer { get; set; }

    public HttpClient? HttpClient { get; private set; }
    private OpenIdentityStackTestSeeder? TestSeeder { get; set; }

    public async ValueTask InitializeAsync()
    {
        this.Connection = new SqliteConnection(ConnectionString);
        await this.Connection.OpenAsync();

        this.PreviousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__openidentitystack");
        this.PreviousOpenIddictIssuer = Environment.GetEnvironmentVariable("OpenIddict__Issuer");
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__openidentitystack",
            ConnectionString);
        Environment.SetEnvironmentVariable(
            "OpenIddict__Issuer",
            "https://issuer.example.com");

        this.TestSeeder = await OpenIdentityStackTestSeeder.CreateAsync(ConnectionString);

        this.Factory = new OpenIdentityStackApiFactory();
        this.HttpClient = this.CreateClient();
        this.HttpClient.Timeout = RequestTimeout;
    }

    /// <summary>
    /// Creates a test service account using the test seeder.
    /// </summary>
    /// <param name="clientId">The client ID for the service account.</param>
    /// <param name="clientSecret">The client secret for the service account.</param>
    /// <param name="allowedScopes">Optional scopes the client can request.</param>
    /// <param name="allowedGrantTypes">Optional grant types (defaults to client_credentials).</param>
    /// <param name="redirectUris">Optional redirect URIs for authorization code flow.</param>
    public async Task CreateServiceAccountAsync(
        string clientId,
        string clientSecret,
        IReadOnlyList<string>? allowedScopes = null,
        IReadOnlyList<string>? allowedGrantTypes = null,
        IReadOnlyList<string>? redirectUris = null)
    {
        if (this.TestSeeder is null)
        {
            throw new InvalidOperationException("TestSeeder is not initialized.");
        }

        await this.TestSeeder.CreateServiceAccountAsync(
            clientId,
            clientSecret,
            allowedScopes,
            allowedGrantTypes,
            redirectUris);
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
        
        HttpClient client = this.CreateClient();
        client.Timeout = RequestTimeout;
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateClient(bool allowAutoRedirect = true)
    {
        if (this.Factory is null)
        {
            throw new InvalidOperationException("Factory is not initialized.");
        }

        return this.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            BaseAddress = new Uri("https://issuer.example.com")
        });
    }

    public async Task ExecuteDbContextAsync(Func<OpenIdentityStackDbContext, Task> operation)
    {
        if (this.Factory is null)
        {
            throw new InvalidOperationException("Factory is not initialized.");
        }

        using IServiceScope scope = this.Factory.Services.CreateScope();
        OpenIdentityStackDbContext dbContext = scope.ServiceProvider.GetRequiredService<OpenIdentityStackDbContext>();
        await operation(dbContext);
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
    public async Task<Guid> CreateTestUserAsync(
        string email,
        string displayName,
        string password,
        OpenIdentityStack.Domain.Users.UserProfileData? profile = null)
    {
        if (this.TestSeeder is null)
        {
            throw new InvalidOperationException("TestSeeder is not initialized.");
        }

        return await this.TestSeeder.CreateTestUserAsync(email, displayName, password, profile);
    }

    public async Task DisableUserAsync(Guid userId, string reason = "Test disabled user")
    {
        if (this.TestSeeder is null)
        {
            throw new InvalidOperationException("TestSeeder is not initialized.");
        }

        await this.TestSeeder.DisableUserAsync(userId, reason);
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
    /// Gets the effective roles for a user via the test seeding API.
    /// </summary>
    public async Task<IReadOnlyList<(Guid Id, string Name, string DisplayName)>> GetUserRolesAsync(Guid userId)
    {
        if (this.TestSeeder is null)
        {
            throw new InvalidOperationException("TestSeeder is not initialized.");
        }

        return await this.TestSeeder.GetUserRolesAsync(userId);
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
        if (this.Factory is not null)
        {
            await this.Factory.DisposeAsync();
        }
        if (this.Connection is not null)
        {
            await this.Connection.DisposeAsync();
        }
        Environment.SetEnvironmentVariable("ConnectionStrings__openidentitystack", this.PreviousConnectionString);
        Environment.SetEnvironmentVariable("OpenIddict__Issuer", this.PreviousOpenIddictIssuer);
        GC.SuppressFinalize(this);
    }

    private sealed class OpenIdentityStackApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:openidentitystack"] = ConnectionString,
                    ["OpenIddict:Issuer"] = "https://issuer.example.com"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<OpenIdentityStackDbContext>>();
                services.RemoveAll<OpenIdentityStackDbContext>();

                services.AddDbContext<OpenIdentityStackDbContext>(options =>
                {
                    options.UseSqlite(ConnectionString);
                    options.UseOpenIddict();
                });

                services.PostConfigure<HealthCheckServiceOptions>(options =>
                {
                    HealthCheckRegistration? postgresqlHealthCheck = options.Registrations
                        .FirstOrDefault(registration => string.Equals(registration.Name, "postgresql", StringComparison.OrdinalIgnoreCase));

                    if (postgresqlHealthCheck is not null)
                    {
                        options.Registrations.Remove(postgresqlHealthCheck);
                    }
                });
            });
        }
    }
}
