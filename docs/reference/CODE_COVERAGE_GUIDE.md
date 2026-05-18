# Code Coverage Improvement Guide

This guide provides concrete examples and patterns for improving code coverage in the OpenIdentityStack solution.

## Quick Reference

### Running Coverage Locally

```bash
# Run all tests with coverage
dotnet test --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml

# Generate HTML report
reportgenerator -reports:"tests/**/TestResults/coverage.cobertura.xml" \
                -targetdir:"coverage-report" \
                -reporttypes:"Html;TextSummary"

# View report
open coverage-report/index.html  # macOS
xdg-open coverage-report/index.html  # Linux
start coverage-report/index.html  # Windows
```

### Run Specific Test Project

```bash
# Run only infrastructure tests
dotnet test tests/OpenIdentityStack.Infrastructure.Tests --coverage

# Run only domain tests
dotnet test tests/OpenIdentityStack.Domain.Tests --coverage
```

## Test Patterns by Layer

### Domain Layer Tests (Target: 95%+)

Domain tests should be pure unit tests with no dependencies.

#### Example: Entity Creation Test
```csharp
public class ClientTests
{
    [Fact]
    public void CreateClient_WithValidInputs_ShouldSucceed()
    {
        // Arrange
        var clientId = new ClientId(Guid.NewGuid());
        var name = "Test Client";
        var clientType = ClientType.Confidential;

        // Act
        var result = Client.Create(clientId, name, clientType);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var client = result.Value;
        client.Id.ShouldBe(clientId);
        client.Name.ShouldBe(name);
        client.ClientType.ShouldBe(clientType);
    }

    [Fact]
    public void CreateClient_WithEmptyName_ShouldFail()
    {
        // Arrange
        var clientId = new ClientId(Guid.NewGuid());
        var name = "";
        var clientType = ClientType.Confidential;

        // Act
        var result = Client.Create(clientId, name, clientType);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClientErrors.NameRequired);
    }

    [Fact]
    public void AddRedirectUri_WhenValid_ShouldAddToCollection()
    {
        // Arrange
        var client = CreateTestClient();
        var redirectUri = new Uri("https://example.com/callback");

        // Act
        var result = client.AddRedirectUri(redirectUri);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        client.RedirectUris.ShouldContain(redirectUri);
    }

    private Client CreateTestClient()
    {
        var result = Client.Create(
            new ClientId(Guid.NewGuid()),
            "Test Client",
            ClientType.Confidential
        );
        return result.Value;
    }
}
```

#### Example: Value Object Test
```csharp
public class ClaimMappingTests
{
    [Fact]
    public void Create_WithValidInputs_ShouldSucceed()
    {
        // Arrange
        var sourceClaim = "email";
        var targetClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";

        // Act
        var result = ClaimMapping.Create(sourceClaim, targetClaim);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var mapping = result.Value;
        mapping.SourceClaim.ShouldBe(sourceClaim);
        mapping.TargetClaim.ShouldBe(targetClaim);
    }

    [Theory]
    [InlineData("", "target")]
    [InlineData("source", "")]
    [InlineData(null, "target")]
    [InlineData("source", null)]
    public void Create_WithInvalidInputs_ShouldFail(string sourceClaim, string targetClaim)
    {
        // Act
        var result = ClaimMapping.Create(sourceClaim, targetClaim);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }
}
```

### Application Layer Tests (Target: 80%+)

Application tests focus on use case logic with mocked dependencies.

#### Example: Use Case Test
```csharp
public class CreateClientUseCaseTests
{
    private readonly IClientRepository _clientRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly CreateClientUseCase _sut;

    public CreateClientUseCaseTests()
    {
        _clientRepository = Substitute.For<IClientRepository>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        
        _sut = new CreateClientUseCase(_clientRepository, _dateTimeProvider);
    }

    [Fact]
    public async Task Execute_WithValidCommand_ShouldCreateClient()
    {
        // Arrange
        var command = new CreateClientCommand
        {
            Name = "Test Client",
            ClientType = "Confidential",
            RedirectUris = new[] { "https://example.com/callback" }
        };

        // Act
        var result = await _sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ClientId.ShouldNotBeEmpty();
        
        await _clientRepository.Received(1).AddAsync(
            Arg.Is<Client>(c => c.Name == command.Name),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Execute_WithDuplicateName_ShouldFail()
    {
        // Arrange
        var command = new CreateClientCommand { Name = "Existing Client" };
        _clientRepository
            .GetByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(CreateTestClient("Existing Client"));

        // Act
        var result = await _sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("Duplicate");
    }

    [Fact]
    public async Task Execute_WhenRepositoryFails_ShouldReturnError()
    {
        // Arrange
        var command = new CreateClientCommand { Name = "Test" };
        _clientRepository
            .AddAsync(Arg.Any<Client>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Client>(new InvalidOperationException("DB error")));

        // Act
        var result = await _sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    private Client CreateTestClient(string name)
    {
        var result = Client.Create(new ClientId(Guid.NewGuid()), name, ClientType.Confidential);
        return result.Value;
    }
}
```

#### Example: Query Handler Test
```csharp
public class ListClientsQueryHandlerTests
{
    private readonly IClientRepository _clientRepository;
    private readonly ListClientsQueryHandler _sut;

    public ListClientsQueryHandlerTests()
    {
        _clientRepository = Substitute.For<IClientRepository>();
        _sut = new ListClientsQueryHandler(_clientRepository);
    }

    [Fact]
    public async Task Handle_WithNoFilters_ShouldReturnAllClients()
    {
        // Arrange
        var query = new ListClientsQuery { Page = 1, PageSize = 10 };
        var clients = new[]
        {
            CreateTestClient("Client 1"),
            CreateTestClient("Client 2")
        };
        _clientRepository
            .ListAsync(query.Page, query.PageSize, Arg.Any<CancellationToken>())
            .Returns((clients, 2));

        // Act
        var result = await _sut.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldHaveCount(2);
        result.Value.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var query = new ListClientsQuery { Page = 2, PageSize = 5 };
        var clients = new[] { CreateTestClient("Client 6") };
        _clientRepository
            .ListAsync(2, 5, Arg.Any<CancellationToken>())
            .Returns((clients, 10));

        // Act
        var result = await _sut.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Value.Items.ShouldHaveCount(1);
        result.Value.TotalCount.ShouldBe(10);
        result.Value.Page.ShouldBe(2);
    }
}
```

### Infrastructure Layer Tests (Target: 75%+)

Infrastructure tests are integration tests using real EF Core with in-memory or test database.

#### Example: Repository Test
```csharp
public class ClientRepositoryTests : IAsyncLifetime
{
    private OpenIdentityStackDbContext _context;
    private ClientRepository _sut;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<OpenIdentityStackDbContext>()
            .UseInMemoryDatabase($"ClientRepo_{Guid.NewGuid()}")
            .Options;

        _context = new OpenIdentityStackDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _sut = new ClientRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_ShouldPersistClient()
    {
        // Arrange
        var client = CreateTestClient();

        // Act
        await _sut.AddAsync(client, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Assert
        var retrieved = await _sut.GetByIdAsync(client.Id, CancellationToken.None);
        retrieved.ShouldNotBeNull();
        retrieved.Id.ShouldBe(client.Id);
        retrieved.Name.ShouldBe(client.Name);
    }

    [Fact]
    public async Task GetByNameAsync_WhenExists_ShouldReturnClient()
    {
        // Arrange
        var client = CreateTestClient("Unique Name");
        await _sut.AddAsync(client, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByNameAsync("Unique Name", CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Unique Name");
    }

    [Fact]
    public async Task GetByNameAsync_WhenNotExists_ShouldReturnNull()
    {
        // Act
        var result = await _sut.GetByNameAsync("NonExistent", CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyExistingClient()
    {
        // Arrange
        var client = CreateTestClient("Original Name");
        await _sut.AddAsync(client, CancellationToken.None);
        await _context.SaveChangesAsync();
        
        var updateResult = client.UpdateName("Updated Name");
        updateResult.IsSuccess.ShouldBeTrue();

        // Act
        await _sut.UpdateAsync(client, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Assert
        var retrieved = await _sut.GetByIdAsync(client.Id, CancellationToken.None);
        retrieved.Name.ShouldBe("Updated Name");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveClient()
    {
        // Arrange
        var client = CreateTestClient();
        await _sut.AddAsync(client, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Act
        await _sut.DeleteAsync(client, CancellationToken.None);
        await _context.SaveChangesAsync();

        // Assert
        var retrieved = await _sut.GetByIdAsync(client.Id, CancellationToken.None);
        retrieved.ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        for (int i = 1; i <= 15; i++)
        {
            await _sut.AddAsync(CreateTestClient($"Client {i}"), CancellationToken.None);
        }
        await _context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await _sut.ListAsync(2, 5, CancellationToken.None);

        // Assert
        items.ShouldHaveCount(5);
        totalCount.ShouldBe(15);
    }

    private Client CreateTestClient(string name = "Test Client")
    {
        var result = Client.Create(
            new ClientId(Guid.NewGuid()),
            name,
            ClientType.Confidential
        );
        return result.Value;
    }
}
```

### API/Controller Tests (Target: 70%+)

API tests verify HTTP endpoints with Aspire integration.

#### Example: API Integration Test
```csharp
public class ClientsControllerTests : IClassFixture<AppHostFixture>
{
    private readonly HttpClient _client;
    private readonly AppHostFixture _fixture;

    public ClientsControllerTests(AppHostFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateHttpClient("openidentitystack-api");
    }

    [Fact]
    public async Task CreateClient_WithValidData_ShouldReturn201()
    {
        // Arrange
        var request = new CreateClientCommand
        {
            Name = $"Test Client {Guid.NewGuid()}",
            ClientType = "Confidential",
            RedirectUris = new[] { "https://example.com/callback" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/clients", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateClientResult>();
        result.ClientId.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetClient_WhenExists_ShouldReturn200()
    {
        // Arrange
        var createResponse = await CreateTestClient();
        var clientId = createResponse.ClientId;

        // Act
        var response = await _client.GetAsync($"/api/admin/clients/{clientId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var client = await response.Content.ReadFromJsonAsync<ClientDetails>();
        client.Id.ShouldBe(clientId);
    }

    [Fact]
    public async Task GetClient_WhenNotExists_ShouldReturn404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/admin/clients/{nonExistentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListClients_ShouldReturnPaginatedResults()
    {
        // Arrange
        await CreateTestClient();
        await CreateTestClient();

        // Act
        var response = await _client.GetAsync("/api/admin/clients?page=1&pageSize=10");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ListClientsResult>();
        result.Items.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task UpdateClient_WithValidData_ShouldReturn200()
    {
        // Arrange
        var createResponse = await CreateTestClient();
        var updateRequest = new UpdateClientCommand
        {
            ClientId = createResponse.ClientId,
            Name = "Updated Name"
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/admin/clients/{createResponse.ClientId}",
            updateRequest
        );

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteClient_WhenExists_ShouldReturn204()
    {
        // Arrange
        var createResponse = await CreateTestClient();

        // Act
        var response = await _client.DeleteAsync($"/api/admin/clients/{createResponse.ClientId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<CreateClientResult> CreateTestClient()
    {
        var request = new CreateClientCommand
        {
            Name = $"Test Client {Guid.NewGuid()}",
            ClientType = "Confidential",
            RedirectUris = new[] { "https://example.com/callback" }
        };
        var response = await _client.PostAsJsonAsync("/api/admin/clients", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateClientResult>();
    }
}
```

## Coverage Anti-Patterns to Avoid

### ❌ Testing Implementation Details
```csharp
// Bad - testing private methods
[Fact]
public void PrivateMethod_Should...() { /* uses reflection */ }

// Good - test through public API
[Fact]
public void PublicMethod_WithCondition_ShouldBehavior() { }
```

### ❌ Testing Framework Code
```csharp
// Bad - testing EF Core, not your code
[Fact]
public void DbContext_CanSaveChanges() { }

// Good - test your repository logic
[Fact]
public void Repository_SaveClient_ShouldPersist() { }
```

### ❌ Shallow Tests
```csharp
// Bad - tests nothing meaningful
[Fact]
public void Client_Constructor_ShouldNotThrow()
{
    var client = new Client();
    client.ShouldNotBeNull();
}

// Good - tests actual behavior
[Fact]
public void CreateClient_WithInvalidEmail_ShouldReturnValidationError()
{
    var result = Client.Create(id, "invalid-email", ...);
    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Client.InvalidEmail");
}
```

### ❌ Over-Mocking
```csharp
// Bad - mocking everything, testing nothing
[Fact]
public void UseCase_Should...()
{
    var repo = Substitute.For<IRepo>();
    repo.Get().Returns(substitute.For<Entity>());
    // Test tells you nothing
}

// Good - use real objects where possible
[Fact]
public void UseCase_WithValidEntity_ShouldProcess()
{
    var repo = Substitute.For<IRepo>();
    repo.Get().Returns(CreateRealEntity());  // Real entity
    // Now testing actual logic
}
```

## Tips for High Coverage

### 1. Test Edge Cases
- Null/empty inputs
- Boundary values (0, -1, max)
- Special characters
- Large datasets
- Concurrent access

### 2. Use Theory Tests
```csharp
[Theory]
[InlineData("", ClientErrors.NameRequired)]
[InlineData("   ", ClientErrors.NameRequired)]
[InlineData(null, ClientErrors.NameRequired)]
[InlineData("A", ClientErrors.NameTooShort)]
public void CreateClient_WithInvalidName_ShouldReturnError(string name, DomainError expectedError)
{
    var result = Client.Create(id, name, type);
    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(expectedError);
}
```

### 3. Cover Error Paths
```csharp
[Fact]
public async Task CreateClient_WhenDatabaseFails_ShouldReturnError()
{
    _repository.AddAsync(Arg.Any<Client>(), Arg.Any<CancellationToken>())
        .Returns(Task.FromException<Client>(new DbUpdateException()));
        
    var result = await _sut.ExecuteAsync(command, CancellationToken.None);
    
    result.IsFailure.ShouldBeTrue();
}
```

### 4. Test Validation Logic
```csharp
[Fact]
public void ValidateCommand_WithMissingRequiredFields_ShouldFail()
{
    var command = new CreateClientCommand(); // Empty
    
    var validator = new CreateClientCommandValidator();
    var result = validator.Validate(command);
    
    result.IsValid.ShouldBeFalse();
    result.Errors.ShouldContain(e => e.PropertyName == nameof(command.Name));
}
```

## Coverage Tracking

### Local Development
```bash
# Generate and view coverage
./scripts/coverage.sh

# Or manually
dotnet test --coverage
reportgenerator -reports:tests/**/coverage.cobertura.xml -targetdir:coverage-report
```

### CI/CD Pipeline
```yaml
# .github/workflows/test.yml
- name: Test with Coverage
  run: dotnet test --coverage --coverage-output coverage.cobertura.xml
  
- name: Coverage Report
  run: |
    reportgenerator \
      -reports:tests/**/coverage.cobertura.xml \
      -targetdir:coverage-report \
      -reporttypes:Html;Badges
      
- name: Upload to Codecov
  uses: codecov/codecov-action@v3
  with:
    files: tests/**/coverage.cobertura.xml
```

## Next Steps

1. **Start with Domain Layer** - Already at 91%, push to 95%
2. **Fix Test Infrastructure** - Stabilize Aspire tests
3. **Client Management** - Zero coverage, highest business value
4. **Repositories** - Core infrastructure, needed for all features
5. **Continuous Improvement** - Make coverage part of DoD

## Resources

- [xUnit Documentation](https://xunit.net/)
- [NSubstitute Guide](https://nsubstitute.github.io/help.html)
- [Shouldly Assertions](https://docs.shouldly.org/)
- [EF Core Testing](https://learn.microsoft.com/ef/core/testing/)
- [ReportGenerator](https://reportgenerator.io/)

---

**Note:** This is a living document. Update with new patterns as you discover them.
