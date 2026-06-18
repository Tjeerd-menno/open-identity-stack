# Migration Guide: Converting to True Contract Tests

## Overview

This guide helps you convert existing "contract tests" (which are actually integration tests) into true contract tests that validate API schema compliance without testing business logic.

## Quick Reference

### Before (Incorrect "Contract" Test)
```csharp
[Fact]
public async Task CreateUser_ThenVerifyPassword_Works()
{
    var request = new { Email = "test@example.com", Password = "Pass123!" };
    var response = await client.PostAsJsonAsync("/api/admin/users", request);
    var userId = GetId(response);
    
    // ❌ This is testing business logic, not contract!
    await _fixture.ValidateUserCredentialsAsync(request.Email, request.Password);
}
```

### After (Correct Contract Test)
```csharp
[Fact]
public async Task CreateUser_ValidRequest_ReturnsCorrectSchema()
{
    var request = new { Email = "test@example.com", Password = "Pass123!" };
    var response = await client.PostAsJsonAsync("/api/admin/users", request);
    
    // ✅ Just validate the contract (status, schema)
    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    var json = await response.Content.ReadFromJsonAsync<JsonNode>();
    json["id"].ShouldNotBeNull();
    json["email"].ShouldNotBeNull();
}
```

## Step-by-Step Migration

### Step 1: Identify Tests to Migrate vs. Move

Review each test in `*.Contract.Tests` and categorize:

**Keep in Contract.Tests** (Validates contract only):
- Status code verification
- Response schema structure
- Required field presence
- Field type validation
- Authentication requirements (401/403)
- Error response formats

**Move to Api.Tests** (Tests business logic):
- Create → Read → Update → Delete workflows
- Business rule enforcement
- Data persistence verification
- Multi-step operations
- Side effect verification
- Credential validation
- Complex scenarios

### Step 2: Refactor Contract Tests

For tests staying in Contract.Tests, remove business logic:

#### Example 1: User Creation

**Before:**
```csharp
[Fact]
public async Task CreateUser_WithValidData_Works()
{
    var request = new { Email = "test@example.com", DisplayName = "Test", Password = "Pass123!" };
    var response = await client.PostAsJsonAsync("/api/admin/users", request);
    
    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    var userId = GetId(response);
    
    // ❌ Business logic - doesn't belong in contract test
    var getResponse = await client.GetAsync($"/api/admin/users/{userId}");
    var user = await getResponse.Content.ReadFromJsonAsync<UserResponse>();
    user.Email.ShouldBe(request.Email);
}
```

**After:**
```csharp
[Fact]
public async Task CreateUser_ValidRequest_ReturnsCorrectSchema()
{
    var request = new { Email = "test@example.com", DisplayName = "Test", Password = "Pass123!" };
    var response = await client.PostAsJsonAsync("/api/admin/users", request);
    
    // ✅ Contract validation only
    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    response.Headers.Location.ShouldNotBeNull();
    
    var json = await response.Content.ReadFromJsonAsync<JsonNode>();
    json.ShouldNotBeNull();
    json["id"].ShouldNotBeNull();
    json["id"]!.GetValue<Guid>().ShouldNotBe(Guid.Empty);
    json["email"].ShouldNotBeNull();
    json["displayName"].ShouldNotBeNull();
    json["status"].ShouldNotBeNull();
    json["createdAt"].ShouldNotBeNull();
    
    // Removed: Second GET request - that tests business logic
}
```

#### Example 2: Password Reset

**Before:**
```csharp
[Fact]
public async Task ResetPassword_Works()
{
    var (userId, email) = await CreateUserAsync();
    var newPassword = "NewPass123!";
    
    var response = await client.PostAsJsonAsync(
        $"/api/admin/users/{userId}/reset-password",
        new { NewPassword = newPassword }
    );
    
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    
    // ❌ Business logic validation
    await _fixture.ValidateUserCredentialsAsync(email, newPassword);
}
```

**After (Contract Test):**
```csharp
[Fact]
public async Task ResetPassword_ValidRequest_Returns200()
{
    var userId = Guid.NewGuid(); // Don't care if it exists
    var request = new { NewPassword = "NewPass123!" };
    
    var response = await client.PostAsJsonAsync(
        $"/api/admin/users/{userId}/reset-password",
        request
    );
    
    // ✅ Just verify status code (contract)
    // Actual response could be 200 or 404 depending on if user exists
    // Contract test: both are valid status codes per spec
    (response.StatusCode == HttpStatusCode.OK || 
     response.StatusCode == HttpStatusCode.NotFound).ShouldBeTrue();
}
```

**Create Integration Test Instead:**
```csharp
// In OpenIdentityStack.Api.Tests/Admin/UsersControllerTests.cs
[Fact]
public async Task ResetPassword_AllowsLoginWithNewPassword()
{
    var (userId, email) = await CreateUserAsync();
    var newPassword = "NewPass123!";
    
    await client.PostAsJsonAsync(
        $"/api/admin/users/{userId}/reset-password",
        new { NewPassword = newPassword }
    );
    
    // ✅ Integration test validates business logic
    await _fixture.ValidateUserCredentialsAsync(email, newPassword);
}
```

### Step 3: Use OpenApiContractValidator

Add automated schema validation:

```csharp
private OpenApiContractValidator? _contractValidator;

public async ValueTask InitializeAsync()
{
    _client = _fixture.HttpClient!;
    
    var specPath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "..", "..", "..", "..", "..", "specs", "001-openiddict-iam", "contracts", "admin-api.yaml"
    );
    
    _contractValidator = await OpenApiContractValidator.LoadFromFileAsync(specPath);
}

[Fact]
public async Task CreateUser_DefinesPactContract()
{
    // Define consumer expectation
    var pact = PactHelper.CreatePactBuilder("ManagementWeb", "OpenIdentityStack.Api")
        .UponReceiving("A request to create a user")
            .WithRequest(HttpMethod.Post, "/api/admin/users")
            .WithJsonBody(new { email = "test@example.com", displayName = "Test" })
        .WillRespond()
            .WithStatus(201)
            .WithJsonBody(new
            {
                id = PactHelper.Matchers.Guid(),
                email = Match.Type("test@example.com")
            });
    
    // Verify using mock server
    await pact.VerifyAsync(async ctx =>
    {
        var response = await client.PostAsJsonAsync("/api/admin/users", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    });
}
```

### Step 4: Update Test Names

Rename tests to reflect their contract-focused purpose:

**Before:**
- `CreateUser_ThenGetUser_ReturnsCreatedUser`
- `DisableUser_ThenEnable_Works`
- `ResetPassword_AllowsLogin`

**After:**
- `CreateUser_ValidRequest_ReturnsCorrectSchema`
- `CreateUser_ValidRequest_Returns201Created`
- `DisableUser_ValidRequest_Returns200Ok`
- `ResetPassword_ValidRequest_Returns200Ok`

### Step 5: Remove Test Helpers That Do Business Logic

**Remove from Contract.Tests:**
```csharp
// ❌ Remove - this tests business logic
private async Task<(Guid, string)> CreateUserAsync()
{
    var response = await client.PostAsJsonAsync("/api/admin/users", request);
    var json = await response.Content.ReadFromJsonAsync<JsonNode>();
    return (json["id"].GetValue<Guid>(), json["email"].GetValue<string>());
}
```

**Keep Simple Helpers:**
```csharp
// ✅ Keep - just creates auth client
private async Task<HttpClient> CreateAuthenticatedClientAsync()
{
    var clientId = $"test-{Guid.NewGuid():N}";
    await _fixture.CreateServiceAccountAsync(clientId, "secret");
    var token = await _fixture.GetAccessTokenAsync(clientId, "secret");
    // ... return client with auth header
}
```

## Migration Checklist

For each test file in `*.Contract.Tests`:

- [ ] Review each test - does it test business logic?
- [ ] If yes, move to corresponding `*.Api.Tests` file
- [ ] If no, simplify to only validate contract (status, schema)
- [ ] Remove database verification
- [ ] Remove multi-step workflows
- [ ] Remove credential validation
- [ ] Add Pact contract definitions
- [ ] Use PactHelper for matchers
- [ ] Remove business logic helpers
- [ ] Run tests to ensure they pass

## Common Patterns

### Pattern 1: Validation Errors

**Contract Test:**
```csharp
[Fact]
public async Task CreateUser_MissingEmail_Returns400()
{
    var request = new { DisplayName = "Test" }; // Missing email
    var response = await client.PostAsJsonAsync("/api/admin/users", request);
    
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
}
```

### Pattern 2: Authentication

**Contract Test:**
```csharp
[Fact]
public async Task ListUsers_NoAuth_Returns401()
{
    var response = await unauthenticatedClient.GetAsync("/api/admin/users");
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
}
```

### Pattern 3: Resource Not Found

**Contract Test:**
```csharp
[Fact]
public async Task GetUser_NonExistent_Returns404()
{
    var response = await client.GetAsync($"/api/admin/users/{Guid.NewGuid()}");
    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
}
```

### Pattern 4: Schema Validation

**Contract Test:**
```csharp
[Fact]
public async Task ListUsers_ReturnsValidPaginationSchema()
{
    var response = await client.GetAsync("/api/admin/users");
    
    var json = await response.Content.ReadFromJsonAsync<JsonNode>();
    json.ShouldNotBeNull();
    json["items"].ShouldNotBeNull();
    json["page"].ShouldNotBeNull();
    json["pageSize"].ShouldNotBeNull();
    json["totalCount"].ShouldNotBeNull();
    
    // Don't verify actual values, just schema
}
```

## Example: Complete File Migration

See `tests/OpenIdentityStack.Contract.Tests/Examples/UserContractExamples.cs` for complete examples of properly written contract tests.

## Questions?

If you're unsure whether a test belongs in Contract.Tests or Api.Tests, ask:

1. **Does it verify the API matches its specification?** → Contract.Tests
2. **Does it verify business logic works correctly?** → Api.Tests
3. **Does it verify data persistence?** → Api.Tests
4. **Does it test a multi-step workflow?** → Api.Tests
5. **Does it just check status codes and schemas?** → Contract.Tests

When in doubt, err on the side of moving it to Api.Tests. Integration tests are more valuable than duplicate contract tests.

## Further Reading

- [Testing Strategy Documentation](TESTING-STRATEGY.md)
- [Contract Tests README](https://github.com/Tjeerd-menno/open-identity-stack/blob/main/tests/OpenIdentityStack.Contract.Tests/README.md)
- [Integration Tests README](https://github.com/Tjeerd-menno/open-identity-stack/blob/main/tests/OpenIdentityStack.Api.Tests/README.md)
- [Example Contract Tests](https://github.com/Tjeerd-menno/open-identity-stack/blob/main/tests/OpenIdentityStack.Contract.Tests/Examples/UserContractExamples.cs)
