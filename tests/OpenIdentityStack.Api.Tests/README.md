# OpenIdentityStack API Integration Tests

## Purpose

These tests validate the **complete business logic and workflows** of the OpenIdentityStack API. They focus on:

- ✅ Complete CRUD workflows work correctly
- ✅ Business rules are enforced
- ✅ Data is persisted correctly
- ✅ Authentication and authorization work
- ✅ Complex multi-step operations succeed
- ✅ Side effects occur as expected

They are **not** concerned with:

- ❌ Exact response schema structure (that's for Contract Tests)
- ❌ OpenAPI specification compliance (that's for Contract Tests)

## Difference from Contract Tests

| Aspect | Integration Tests (`*.Api.Tests`) | Contract Tests (`*.Contract.Tests`) |
|--------|-----------------------------------|-------------------------------------|
| **Purpose** | Validate business logic and workflows | Validate API contract compliance |
| **Database** | Real database operations | Not needed |
| **Focus** | Complete end-to-end functionality | Schema, structure, format |
| **Example** | "Created user can login with new password" | "POST /users returns 201 with correct schema" |
| **Side Effects** | Verify data persistence, events, etc. | Don't verify side effects |

## What Integration Tests Validate

### 1. Complete CRUD Workflows

```csharp
[Fact]
public async Task CreateUser_ThenGetUser_ReturnsCreatedUser()
{
    // Create
    var createResponse = await client.PostAsJsonAsync("/api/admin/users", request);
    var userId = GetIdFromResponse(createResponse);
    
    // Read - Verify user was actually created in database
    var getResponse = await client.GetAsync($"/api/admin/users/{userId}");
    var user = await getResponse.Content.ReadFromJsonAsync<UserResponse>();
    
    // Assert business logic worked
    user.Email.ShouldBe(request.Email);
    user.DisplayName.ShouldBe(request.DisplayName);
}
```

### 2. Business Rule Enforcement

```csharp
[Fact]
public async Task CreateUser_WithDuplicateEmail_Returns409()
{
    // First user succeeds
    await client.PostAsJsonAsync("/api/admin/users", request);
    
    // Duplicate email should fail - business rule enforcement
    var response = await client.PostAsJsonAsync("/api/admin/users", request);
    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
}
```

### 3. Data Persistence

```csharp
[Fact]
public async Task UpdateUser_ChangesDisplayName()
{
    var userId = await CreateUserAsync();
    var update = new { DisplayName = "Updated Name" };
    
    await client.PutAsJsonAsync($"/api/admin/users/{userId}", update);
    
    // Verify change was persisted
    var getResponse = await client.GetAsync($"/api/admin/users/{userId}");
    var user = await getResponse.Content.ReadFromJsonAsync<UserResponse>();
    user.DisplayName.ShouldBe("Updated Name");
}
```

### 4. Complex Multi-Step Operations

```csharp
[Fact]
public async Task DisableUser_ThenEnable_RestoresAccess()
{
    // Setup: Create and verify user
    var userId = await CreateUserAsync();
    await _fixture.VerifyUserAsync(userId);
    
    // Disable
    await client.PostAsJsonAsync($"/api/admin/users/{userId}/disable", new { Reason = "Test" });
    
    // Enable
    await client.PostAsync($"/api/admin/users/{userId}/enable", null);
    
    // Verify user can access system again (business logic)
    var user = await GetUserAsync(userId);
    user.Status.ShouldBe(UserStatus.Active);
}
```

### 5. Authentication & Authorization

```csharp
[Fact]
public async Task CreateUser_WithoutPermission_Returns403()
{
    var limitedClient = await CreateClientWithPermissionsAsync("users:read");
    
    var response = await limitedClient.PostAsJsonAsync("/api/admin/users", request);
    
    // Verify authorization logic works
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
}
```

### 6. Functional Correctness

```csharp
[Fact]
public async Task ResetPassword_AllowsLoginWithNewPassword()
{
    var (userId, email) = await CreateUserAsync();
    var newPassword = "NewPassword123!";
    
    // Reset password
    await client.PostAsJsonAsync(
        $"/api/admin/users/{userId}/reset-password",
        new { NewPassword = newPassword }
    );
    
    // Verify new password actually works (business logic!)
    await _fixture.ValidateUserCredentialsAsync(email, newPassword);
}
```

## Test Patterns

### Using AppHostFixture

All integration tests use `AppHostFixture` which provides:
- In-process API running via `WebApplicationFactory<Program>`
- Assembly-level in-memory SQLite database, prefilled once
- Authentication helpers
- Test data seeding

```csharp
public sealed class UsersControllerTests
{
    private readonly AppHostFixture _fixture;
    
    public UsersControllerTests(AppHostFixture fixture)
    {
        _fixture = fixture;
    }
    
    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        return await _fixture.CreateAuthenticatedClientAsync(
            "test-client",
            "test-secret"
        );
    }
}
```

### Helper Methods

Create helpers for common operations:

```csharp
private static async Task<(Guid UserId, string Email)> CreateUserAsync(HttpClient client)
{
    var email = $"user-{Guid.NewGuid():N}@example.com";
    var request = new
    {
        Email = email,
        DisplayName = "Test User",
        Password = "TestPassword123!"
    };
    
    var response = await client.PostAsJsonAsync("/api/admin/users", request);
    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    
    var json = await response.Content.ReadFromJsonAsync<JsonNode>();
    var id = json["id"].GetValue<Guid>();
    
    return (id, email);
}
```

## Running the Tests

```bash
# Run all integration tests
dotnet test tests/OpenIdentityStack.Api.Tests

# Run specific test class
dotnet test --filter "FullyQualifiedName~UsersControllerTests"

# Run with detailed output
dotnet test tests/OpenIdentityStack.Api.Tests --logger "console;verbosity=detailed"
```

## Test Organization

```
OpenIdentityStack.Api.Tests/
├── Admin/                    # Admin API tests
│   ├── UsersControllerTests.cs
│   ├── ServiceAccountsControllerTests.cs
│   └── ...
├── Authentication/           # Auth flow tests
│   ├── AccountControllerTests.cs
│   ├── FederatedLoginTests.cs
│   └── ...
├── Authorization/            # Permission tests
├── Common/                   # Shared utilities
├── Fixtures/                 # Test fixtures
│   └── AppHostFixture.cs
└── Helpers/                  # Test helpers
```

## When Integration Tests Fail

Integration test failures indicate **business logic bugs**:

1. **Data Not Persisted**: Create/Update didn't save to database
2. **Business Rule Violated**: Validation or domain rule not enforced
3. **Workflow Broken**: Multi-step operation doesn't work
4. **Side Effect Missing**: Expected event, notification, etc. didn't happen
5. **Authorization Bug**: Permission check not working correctly

## Best Practices

### 1. Test Real Workflows

Focus on how users actually use the API:

```csharp
[Fact]
public async Task AdminCanCreateUser_AssignRole_UserHasPermissions()
{
    // Real workflow: Create user -> Assign role -> Verify permissions
    var userId = await CreateUserAsync();
    await AssignRoleAsync(userId, "user-admin");
    
    var permissions = await GetUserPermissionsAsync(userId);
    permissions.ShouldContain("users:read");
}
```

### 2. Use Realistic Data

Use data that reflects real usage:

```csharp
// ✅ Good - realistic email
var email = $"john.doe-{Guid.NewGuid():N}@example.com";

// ❌ Bad - unrealistic test data
var email = "test@test.test";
```

### 3. Clean Up After Tests

AppHostFixture uses one shared in-memory SQLite database for the full test assembly. Prefer unique IDs, emails, and client IDs in tests:

```csharp
[Fact]
public async Task TestThatCreatesUser()
{
    var userId = await CreateUserAsync();
    
    // Test logic...
    
    // Use unique data because the SQLite database is shared across the assembly
}
```

### 4. Test Error Cases

Don't just test happy path:

```csharp
[Fact]
public async Task UpdateUser_WithInvalidData_Returns400()
{
    var userId = await CreateUserAsync();
    var invalidUpdate = new { DisplayName = "" }; // Invalid
    
    var response = await client.PutAsJsonAsync($"/api/admin/users/{userId}", invalidUpdate);
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
}
```

### 5. Verify Side Effects

Check that operations have expected side effects:

```csharp
[Fact]
public async Task DeleteUser_RemovesFromDatabase()
{
    var userId = await CreateUserAsync();
    
    await client.DeleteAsync($"/api/admin/users/{userId}");
    
    // Verify side effect - user no longer exists
    var getResponse = await client.GetAsync($"/api/admin/users/{userId}");
    getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
}
```

## Performance Considerations

Integration tests are slower than unit tests because they:
- Start the full application
- Use a real SQLite database
- Execute complete request/response cycles

**Optimize by:**
- Running tests in parallel (xUnit does this automatically)
- Reusing AppHostFixture across tests in same class
- Not testing every edge case here (use unit tests for that)

### Redirect Handling

Use the fixture to create clients so requests stay inside the in-process test server:

```csharp
using HttpClient client = _fixture.CreateClient(allowAutoRedirect: false);
```

## See Also

- [Testing Strategy Documentation](../../docs/TESTING-STRATEGY.md)
- [Contract Tests](../OpenIdentityStack.Contract.Tests/README.md) - For schema validation
- [Domain Tests](../OpenIdentityStack.Domain.Tests/) - For unit testing domain logic
- [Application Tests](../OpenIdentityStack.Application.Tests/) - For unit testing use cases
