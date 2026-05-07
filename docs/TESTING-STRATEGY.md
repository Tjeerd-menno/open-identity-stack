# Testing Strategy

## Overview

This document outlines the testing strategy for OpenIdentityStack, clarifying the distinctions between different types of tests and their purposes.

## Test Types

### 1. Unit Tests

**Purpose:** Test individual components in isolation
**Location:** `*.Domain.Tests`, `*.Application.Tests`
**Characteristics:**
- No external dependencies (database, network, file system)
- Fast execution
- Use mocks/stubs for dependencies
- Test domain logic, use cases, and business rules

**Example:**
```csharp
[Fact]
public void CreateUser_WithInvalidEmail_ReturnsError()
{
    var result = User.CreateLocal("not-an-email", "Name", "hash", dateProvider);
    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("User.InvalidEmail");
}
```

### 2. Integration Tests (API Tests)

**Purpose:** Test complete request/response flows including business logic and database operations
**Location:** `*.Api.Tests`
**Characteristics:**
- Test full application stack (controllers → use cases → repositories → database)
- Use real or test database
- Verify business logic correctness
- Test authentication/authorization
- Verify side effects (data persistence, events)
- Slower than unit tests

**Example:**
```csharp
[Fact]
public async Task CreateUser_ThenGetUser_ReturnsCreatedUser()
{
    // Arrange
    HttpClient client = await CreateAuthenticatedClientAsync();
    var request = new { Email = "test@example.com", ... };
    
    // Act - Create user
    var createResponse = await client.PostAsJsonAsync("/api/admin/users", request);
    var userId = GetIdFromResponse(createResponse);
    
    // Act - Retrieve user
    var getResponse = await client.GetAsync($"/api/admin/users/{userId}");
    
    // Assert - Verify business logic worked correctly
    getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    var user = await getResponse.Content.ReadFromJsonAsync<UserResponse>();
    user.Email.ShouldBe("test@example.com");
    // User was actually created in the database
}
```

**What Integration Tests Verify:**
- ✅ Complete CRUD workflows
- ✅ Business rule enforcement
- ✅ Data persistence and retrieval
- ✅ Transaction boundaries
- ✅ Complex multi-step operations
- ✅ Authentication and authorization
- ✅ Error handling and validation

### 3. Contract Tests

**Purpose:** Consumer-driven contract testing using Pact.io to ensure API compatibility
**Location:** `*.Contract.Tests`
**Characteristics:**
- Use Pact.io for consumer-driven contract testing
- Consumers define expectations; providers verify they meet them
- Validate request/response **schemas** (structure, types, required fields)
- Detect **breaking changes** in API contracts before deployment
- Do NOT test business logic or database operations
- Do NOT verify correctness of data
- Focus on API as a **contract** between services
- Bidirectional verification (consumer creates, provider verifies)

**Example:**
```csharp
[Fact]
public async Task GetUser_ReturnsExpectedSchema()
{
    // Arrange - Define consumer expectation
    var pact = PactHelper.CreatePactBuilder("AdminWeb", "OpenIdentityStack.Api")
        .UponReceiving("A request to get a user")
            .WithRequest(HttpMethod.Get, "/api/admin/users/123")
            .WithHeader("Authorization", "Bearer token")
        .WillRespond()
            .WithStatus(200)
            .WithJsonBody(new
            {
                id = PactHelper.Matchers.Guid(),
                email = PactHelper.Matchers.Email(),
                displayName = PactHelper.Matchers.NonEmptyString()
            });
    
    // Act & Assert - Verify using Pact mock server
    await pact.VerifyAsync(async ctx =>
    {
        var client = new HttpClient { BaseAddress = ctx.MockServerUri };
        var response = await client.GetAsync("/api/admin/users/123");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    });
}
```
    var response = await client.PostAsJsonAsync("/api/admin/users", request);
    
    // Assert - Verify HTTP contract
    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    response.Headers.Location.ShouldNotBeNull();
    
    // Assert - Verify response schema matches contract
    var json = await response.Content.ReadFromJsonAsync<JsonNode>();
    json.ShouldNotBeNull();
    json["id"].ShouldNotBeNull(); // Required field per contract
    json["id"].GetValue<Guid>().ShouldNotBe(Guid.Empty); // Correct type
    json["email"].ShouldNotBeNull(); // Required field
    json["email"].GetValue<string>().ShouldNotBeNullOrWhiteSpace(); // Correct type
    json["displayName"].ShouldNotBeNull();
    json["createdAt"].ShouldNotBeNull();
    
    // DO NOT verify business logic like:
    // - User exists in database ❌
    // - User can actually login ❌
    // - Related entities were created ❌
}
```

**What Contract Tests Verify:**
- ✅ HTTP status codes match specification
- ✅ Response headers (Location, Content-Type, etc.)
- ✅ Response body schema (required fields, types)
- ✅ Request validation (missing fields → 400, invalid format → 400)
- ✅ Authentication requirements (no auth → 401)
- ✅ Resource not found → 404
- ✅ Conflict → 409
- ❌ Business logic correctness
- ❌ Data persistence
- ❌ Complex workflows

### 4. End-to-End (E2E) Tests

**Purpose:** Test complete user workflows through the UI
**Location:** `*.E2ETests`, `e2e/`
**Characteristics:**
- Test through browser (Playwright)
- Simulate real user interactions
- Test complete features across multiple pages
- Slowest, most expensive tests

**Example:**
```csharp
[Fact]
public async Task UserCanLoginAndCreateGroup()
{
    await page.GotoAsync("/login");
    await page.FillAsync("#email", "admin@example.com");
    await page.FillAsync("#password", "password");
    await page.ClickAsync("button[type=submit]");
    
    await page.ClickAsync("text=Groups");
    await page.ClickAsync("text=Create Group");
    // ... complete workflow
}
```

## Current Issues

### Problem: Contract Tests Are Actually Integration Tests

**Evidence:**
1. `OpenIdentityStack.Contract.Tests` currently perform full CRUD operations
2. They verify database state and business logic
3. They test complex workflows (disable user → enable user)
4. They are essentially duplicates of `OpenIdentityStack.Api.Tests`

**Example of Incorrect "Contract" Test:**
```csharp
// This is NOT a contract test - it's testing business logic!
[Fact]
public async Task ResetPassword_WithValidRequest_Returns200Ok()
{
    (Guid userId, string email) = await CreateUserAsync();
    var request = new { NewPassword = "NewPassword123!" };
    
    var response = await SendRequestAsync(HttpMethod.Post, $"/api/admin/users/{userId}/reset-password", request);
    
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    
    // ❌ This verifies business logic, not contract!
    await _fixture.ValidateUserCredentialsAsync(email, request.NewPassword);
}
```

**What It Should Be:**
```csharp
// True contract test - only validates schema
[Fact]
public async Task ResetPassword_ValidRequest_Returns200WithCorrectSchema()
{
    var request = new { NewPassword = "NewPassword123!" };
    var userId = Guid.NewGuid(); // Don't care if it exists
    
    var response = await SendRequestAsync(HttpMethod.Post, $"/api/admin/users/{userId}/reset-password", request);
    
    // Just verify the contract (status code, schema)
    // Don't verify if password was actually changed!
    if (response.StatusCode == HttpStatusCode.OK)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        // Verify response schema if specified
    }
}
```

## Recommendations

### For Integration Tests (`*.Api.Tests`)

**Focus on:** Complete workflows, business logic, data persistence
**Keep:**
- ✅ Create → Update → Delete workflows
- ✅ Verify data is stored correctly in database
- ✅ Test complex multi-step operations
- ✅ Verify authentication/authorization logic
- ✅ Test validation rules and error messages

### For Contract Tests (`*.Contract.Tests`)

**Focus on:** API specification compliance, schema validation
**Change to:**
- ✅ Use OpenAPI schema validator
- ✅ Test only HTTP contract (status, headers, schema)
- ✅ Mock or stub backends (don't need real DB)
- ✅ Test all endpoints against OpenAPI spec
- ✅ Focus on breaking change detection
- ❌ Remove business logic verification
- ❌ Remove database state checks
- ❌ Remove complex workflow tests

### Implementation Plan

1. **Add Pact.io Contract Testing**
   - Install PactNet library
   - Create consumer contract tests
   - Set up provider verification
   - Integrate with CI/CD

2. **Refactor Contract Tests**
   - Remove database verification
   - Remove business logic tests
   - Use Pact matchers for flexible validation
   - Focus on consumer expectations

3. **Keep Integration Tests As-Is**
   - These correctly test business logic
   - They provide value by testing complete workflows

4. **Update Documentation**
   - Clarify test purposes in README
   - Add examples to this guide
   - Update test naming conventions

## Tools and Libraries

### For Contract Testing

**Recommended:**
- **PactNet** (v5.0.1) - Consumer-driven contract testing framework for .NET
  - Consumer tests define expectations
  - Provider verification ensures compatibility
  - Supports Pact Broker for centralized contract management

**Example with PactNet:**
```csharp
// Consumer side - define expectations
var pact = PactHelper.CreatePactBuilder("Consumer", "Provider")
    .UponReceiving("A request for a user")
        .WithRequest(HttpMethod.Get, "/api/users/123")
    .WillRespond()
        .WithStatus(200)
        .WithJsonBody(new
        {
            id = PactHelper.Matchers.Guid(),
            email = PactHelper.Matchers.Email()
        });

await pact.VerifyAsync(async ctx =>
{
    var response = await client.GetAsync("/api/users/123");
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
});

// Provider side - verify contract fulfillment
new PactVerifier()
    .ServiceProvider("Provider", "https://localhost:5001")
    .WithFileSource(new FileInfo("./pacts/consumer-provider.json"))
    .Verify();
```

## Summary

| Test Type | Purpose | Tests Business Logic | Tests Schemas | Uses DB | Speed |
|-----------|---------|---------------------|---------------|---------|-------|
| **Unit** | Individual components | ✅ | ❌ | ❌ | ⚡⚡⚡ |
| **Integration (API)** | Complete workflows | ✅ | ✅ | ✅ | ⚡⚡ |
| **Contract** | API specification | ❌ | ✅ | ❌ | ⚡⚡⚡ |
| **E2E** | User workflows | ✅ | ✅ | ✅ | ⚡ |

**Key Principle:** Each test type serves a different purpose. Contract tests should NOT duplicate integration tests.
