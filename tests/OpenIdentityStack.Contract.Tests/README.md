# OpenIdentityStack Contract Tests

## Purpose

These tests use **Pact.io** for consumer-driven contract testing. They focus on:

- ✅ Defining consumer expectations as contracts
- ✅ Validating provider responses match consumer needs
- ✅ Ensuring API changes don't break consumers
- ✅ Documenting real-world API usage patterns
- ✅ Creating bidirectional verification (consumer + provider)

They do **NOT** test:

- ❌ Business logic correctness
- ❌ Data persistence in database
- ❌ Complex workflows
- ❌ Side effects

## Difference from Integration Tests

| Aspect | Contract Tests (`*.Contract.Tests`) | Integration Tests (`*.Api.Tests`) |
|--------|-------------------------------------|-----------------------------------|
| **Purpose** | Validate API contracts between services | Validate business logic |
| **Approach** | Consumer-driven with Pact | End-to-end with real services |
| **Database** | Not needed (uses mock server) | Real database operations |
| **Focus** | Consumer expectations & provider compatibility | Complete workflows |
| **Example** | "GET /users returns expected schema" | "Created user can login and has correct permissions" |

## What is Pact?

Pact is a consumer-driven contract testing framework. Unlike traditional API testing:

- **Consumer-Driven**: Consumers define what they expect from the API
- **Contracts**: Tests generate contract files that providers verify
- **Mock Server**: Pact creates mocks, allowing isolated consumer testing
- **Bidirectional**: Both consumer and provider verify the contract
- **CI/CD Integration**: Contracts can be stored in Pact Broker for automated verification

## How Pact Contract Tests Work

1. **Consumer Test**: Defines expectations and generates contract file
   ```csharp
   var pact = PactHelper.CreatePactBuilder("AdminWeb", "OpenIdentityStack.Api")
       .UponReceiving("A request to get a user")
       .WithRequest(HttpMethod.Get, "/api/admin/users/123")
       .WillRespond()
       .WithStatus(200)
       .WithJsonBody(new { id = Matchers.Guid(), email = Matchers.Email() });
   ```

2. **Contract File**: Generated in `./pacts/` directory as JSON

3. **Provider Verification**: Provider runs tests against all consumer contracts
   ```csharp
   new PactVerifier()
       .ServiceProvider("OpenIdentityStack.Api", "https://localhost:5001")
       .WithFileSource(new FileInfo("./pacts/adminweb-openidentitystack.api.json"))
       .Verify();
   ```

4. **Compatibility Check**: Both sides ensure compatibility before deployment

## Example Contract Test

```csharp
[Fact]
public async Task GetUser_ReturnsExpectedSchema()
{
    // Arrange - Define consumer expectation
    var pact = PactHelper.CreatePactBuilder("AdminWeb", "OpenIdentityStack.Api")
        .UponReceiving("A request to get a user by ID")
            .WithRequest(HttpMethod.Get, "/api/admin/users/123")
            .WithHeader("Authorization", "Bearer token")
        .WillRespond()
            .WithStatus(200)
            .WithJsonBody(new
            {
                id = PactHelper.Matchers.Guid(),
                email = PactHelper.Matchers.Email(),
                displayName = PactHelper.Matchers.NonEmptyString(),
                status = Match.Type("Active"),
                createdAt = PactHelper.Matchers.DateTime()
            });

    // Act & Assert - Verify using Pact mock server
    await pact.VerifyAsync(async ctx =>
    {
        var client = new HttpClient { BaseAddress = ctx.MockServerUri };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token");
        
        var response = await client.GetAsync("/api/admin/users/123");
        
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        // Pact automatically validates response matches contract
    });
}
```

## What to Test

### ✅ DO Test

**Consumer Expectations**
```csharp
[Fact]
public async Task CreateUser_ReturnsCreatedStatus()
{
    var pact = PactHelper.CreatePactBuilder("AdminWeb", "OpenIdentityStack.Api")
        .UponReceiving("A request to create a user")
            .WithRequest(HttpMethod.Post, "/api/admin/users")
            .WithJsonBody(new { email = "user@example.com", displayName = "User" })
        .WillRespond()
            .WithStatus(201)
            .WithHeader("Location", Match.Regex("/api/admin/users/123", "/api/admin/users/.+"));
    
    await pact.VerifyAsync(async ctx => { /* ... */ });
}
```

**Error Responses**
```csharp
[Fact]
public async Task CreateUser_InvalidEmail_Returns400()
{
    var pact = PactHelper.CreatePactBuilder("AdminWeb", "OpenIdentityStack.Api")
        .UponReceiving("A request with invalid email")
            .WithRequest(HttpMethod.Post, "/api/admin/users")
            .WithJsonBody(new { email = "invalid", displayName = "User" })
        .WillRespond()
            .WithStatus(400)
            .WithHeader("Content-Type", "application/problem+json");
}
```

**Authentication**
```csharp
[Fact]
public async Task ListUsers_NoAuth_Returns401()
{
    var pact = PactHelper.CreatePactBuilder("AdminWeb", "OpenIdentityStack.Api")
        .UponReceiving("A request without authentication")
            .WithRequest(HttpMethod.Get, "/api/admin/users")
        .WillRespond()
            .WithStatus(401);
}
```

**Response Schemas**
```csharp
[Fact]
public async Task ListUsers_ReturnsPaginatedResponse()
{
    var pact = PactHelper.CreatePactBuilder("AdminWeb", "OpenIdentityStack.Api")
        .UponReceiving("A request for paginated users")
            .WithRequest(HttpMethod.Get, "/api/admin/users")
        .WillRespond()
            .WithJsonBody(new
            {
                items = Match.MinType(0, new { id = Matchers.Guid() }),
                page = Match.Integer(1),
                pageSize = Match.Integer(10),
                totalCount = Matchers.Integer()
            });
}
```

### ❌ DON'T Test

**Business Logic**
```csharp
// ❌ DON'T - This is testing business logic, not contract
[Fact]
public async Task CreateUser_ThenGet_ReturnsCreatedUser()
{
    var createResponse = await client.PostAsJsonAsync("/api/admin/users", request);
    var userId = GetId(createResponse);
    
    var getResponse = await client.GetAsync($"/api/admin/users/{userId}");
    var user = await getResponse.Content.ReadFromJsonAsync<User>();
    
    user.Email.ShouldBe(request.Email); // ❌ Testing business logic
}
```

**Database Operations**
```csharp
// ❌ DON'T - This is testing persistence, not contract
[Fact]
public async Task CreateUser_IsPersisted()
{
    await client.PostAsJsonAsync("/api/admin/users", request);
    var usersInDb = await dbContext.Users.ToListAsync();
    usersInDb.ShouldContain(u => u.Email == request.Email); // ❌ Testing DB
}
```

**Complex Workflows**
```csharp
// ❌ DON'T - This is an integration test scenario
[Fact]
public async Task CreateUser_Verify_Disable_Enable_Works()
{
    // Create -> Verify -> Disable -> Enable
    // This tests business workflow, not just contract
}
```

## Running the Tests

```bash
# Run all contract tests
dotnet test tests/OpenIdentityStack.Contract.Tests

# Run specific test
dotnet test --filter "FullyQualifiedName~UserContractExamples"
```

After tests run, check the `./pacts` directory for generated contract files.

## Provider Verification

Providers should verify all consumer contracts:

```csharp
[Fact]
public void VerifyProviderContracts()
{
    var config = new PactVerifierConfig
    {
        Outputters = new[] { new XUnitOutput(output) }
    };

    new PactVerifier(config)
        .ServiceProvider("OpenIdentityStack.Api", "https://localhost:5001")
        .WithFileSource(new FileInfo("./pacts/adminweb-openidentitystack.api.json"))
        .WithProviderStateUrl(new Uri("https://localhost:5001/provider-states"))
        .Verify();
}
```

## When Contract Tests Fail

Contract test failures indicate **breaking changes** between consumer and provider:

1. **Consumer Test Failure**: Mock server doesn't respond as expected
   - Fix: Update consumer expectations or fix provider implementation
   
2. **Provider Verification Failure**: Provider doesn't fulfill consumer contract
   - Fix: Update provider to meet contract, or work with consumer to update expectations
   
3. **Schema Mismatch**: Response structure doesn't match expected format
   - Fix: Align provider response with consumer expectations

4. **Missing Field**: Expected field not in response
   - Fix: Add field to provider response or remove from consumer contract

## Benefits of Pact

1. **Catch Breaking Changes Early**: Detect incompatibilities before deployment
2. **Consumer-Driven**: API evolves based on actual consumer needs
3. **Bidirectional Verification**: Both consumer and provider verify contracts
4. **Fast Feedback**: Mock-based tests run quickly without full stack
5. **Living Documentation**: Contracts document real usage patterns
6. **CI/CD Integration**: Pact Broker enables automated compatibility checks
7. **Independent Development**: Teams can work independently and verify compatibility

## Pact vs OpenAPI

| Aspect | Pact | OpenAPI Validation |
|--------|------|-------------------|
| **Approach** | Consumer-driven, example-based | Provider-driven, schema-based |
| **Verification** | Bidirectional (consumer + provider) | Unidirectional (against spec) |
| **Coverage** | Actual consumer scenarios | All possible scenarios in spec |
| **Flexibility** | Matchers allow flexible validation | Strict schema compliance |
| **Focus** | Real integration patterns | Technical API specification |

## See Also

- [Testing Strategy Documentation](../../../docs/TESTING-STRATEGY.md)
- [Integration Tests](../OpenIdentityStack.Api.Tests/README.md) - For business logic testing
- [Pact.io Documentation](https://docs.pact.io/)
- [PactNet GitHub](https://github.com/pact-foundation/pact-net)
- [Example Tests](./Examples/UserContractExamples.cs)
