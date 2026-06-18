using PactNet;
using PactNet.Matchers;

namespace OpenIdentityStack.Contract.Tests.Common;

/// <summary>
/// Helper for creating Pact contract tests.
/// Pact enables consumer-driven contract testing where consumers define expectations
/// and providers verify they meet those expectations.
/// 
/// Use this for:
/// - Testing API contracts between services
/// - Ensuring API changes don't break consumers
/// - Documenting expected API behavior through tests
/// </summary>
public static class PactHelper
{
    /// <summary>
    /// Creates a Pact builder for consumer-side contract testing.
    /// </summary>
    /// <param name="consumerName">Name of the consumer (e.g., "ManagementWeb", "IsotopesWeb")</param>
    /// <param name="providerName">Name of the provider (e.g., "OpenIdentityStack.Api", "TraceableIsotopes.Api")</param>
    /// <param name="pactDir">Optional directory to store pact files (defaults to "pacts")</param>
    /// <returns>A configured Pact builder</returns>
    public static IPactBuilderV4 CreatePactBuilder(
        string consumerName,
        string providerName,
        string? pactDir = null)
    {
        string pactDirectory = pactDir ?? Path.Combine(Directory.GetCurrentDirectory(), "pacts");
        
        var config = new PactConfig
        {
            PactDir = pactDirectory,
            LogLevel = PactLogLevel.Debug
        };

        return (IPactBuilderV4)Pact.V4(consumerName, providerName, config);
    }

    /// <summary>
    /// Common matchers for typical API responses.
    /// </summary>
    public static class Matchers
    {
        /// <summary>
        /// Matcher for a GUID/UUID field.
        /// </summary>
        public static IMatcher UniqueId() => Match.Regex(
            "00000000-0000-0000-0000-000000000000",
            "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"
        );

        /// <summary>
        /// Matcher for an ISO 8601 datetime field.
        /// </summary>
        public static IMatcher DateTime() => Match.Regex(
            "2024-01-01T00:00:00Z",
            "^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}(\\.\\d+)?Z?$"
        );

        /// <summary>
        /// Matcher for an email address field.
        /// </summary>
        public static IMatcher Email() => Match.Regex(
            "user@example.com",
            "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$"
        );

        /// <summary>
        /// Matcher for a non-empty string field.
        /// </summary>
        public static IMatcher NonEmptyString() => Match.Type("example");

        /// <summary>
        /// Matcher for an integer field.
        /// </summary>
        public static IMatcher IntegerValue() => Match.Integer(0);

        /// <summary>
        /// Matcher for a boolean field.
        /// </summary>
        public static IMatcher BooleanValue() => Match.Type(true);

        /// <summary>
        /// Matcher for an array with minimum length.
        /// </summary>
        public static IMatcher ArrayMinLength(object example, int minLength) => Match.MinType(example, minLength);
    }
}

/// <summary>
/// Example usage documentation for Pact contract tests.
/// 
/// Consumer-side test example:
/// <code>
/// [Fact]
/// public async Task GetUser_ReturnsUserSchema()
/// {
///     var pact = PactHelper.CreatePactBuilder("ManagementWeb", "OpenIdentityStack.Api")
///         .UponReceiving("A request for a user")
///             .WithRequest(HttpMethod.Get, "/api/admin/users/123")
///             .WithHeader("Authorization", "Bearer token")
///         .WillRespond()
///             .WithStatus(200)
///             .WithJsonBody(new
///             {
///                 id = PactHelper.Matchers.Guid(),
///                 email = PactHelper.Matchers.Email(),
///                 displayName = PactHelper.Matchers.NonEmptyString(),
///                 status = Match.Regex("Active", "^(Active|Disabled|PendingVerification)$"),
///                 createdAt = PactHelper.Matchers.DateTime()
///             });
/// 
///     await pact.VerifyAsync(async ctx =>
///     {
///         var client = new HttpClient { BaseAddress = ctx.MockServerUri };
///         client.DefaultRequestHeaders.Authorization = 
///             new AuthenticationHeaderValue("Bearer", "token");
///         
///         var response = await client.GetAsync("/api/admin/users/123");
///         
///         response.StatusCode.ShouldBe(HttpStatusCode.OK);
///         var user = await response.Content.ReadFromJsonAsync&lt;UserResponse&gt;();
///         user.ShouldNotBeNull();
///     });
/// }
/// </code>
/// 
/// Provider-side verification:
/// <code>
/// [Fact]
/// public void VerifyProviderContracts()
/// {
///     var config = new PactVerifierConfig
///     {
///         Outputters = new[] { new XUnitOutput(output) }
///     };
/// 
///     new PactVerifier(config)
///         .ServiceProvider("OpenIdentityStack.Api", "https://localhost:5001")
///         .WithPactBrokerSource(new Uri("https://your-pact-broker.com"))
///         // OR
///         .WithFileSource(new FileInfo("./pacts/adminweb-openidentitystack.api.json"))
///         .WithProviderStateUrl(new Uri("https://localhost:5001/provider-states"))
///         .Verify();
/// }
/// </code>
/// </summary>
public static class PactUsageExamples
{
    // This class exists solely for documentation purposes
}
