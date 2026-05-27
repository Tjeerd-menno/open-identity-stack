# OpenIdentityStack - Copilot Instructions

## Project Overview

OpenIdentityStack is an OpenIddict-based Identity & Access Management (IAM) product built with .NET 10, .NET Aspire, PostgreSQL, and React. It provides OAuth 2.0/OIDC authentication, RBAC, federation, admin APIs, a DbMigrator, an admin web UI, Linux container images, and Windows service packaging.

## Architecture

This is a **Clean Architecture** solution with four layers:

```
Domain → Application → Infrastructure → Api
```

- **Domain** (`src/OpenIdentityStack.Domain/`): Entities, value objects, domain events, strongly-typed IDs. No external dependencies.
- **Application** (`src/OpenIdentityStack.Application/`): Use cases as interfaces (e.g., `ICreateUserUseCase`), query handler interfaces, repository abstractions.
- **Infrastructure** (`src/OpenIdentityStack.Infrastructure/`): EF Core DbContext, repository implementations, OpenIddict configuration, external services.
- **Api** (`src/OpenIdentityStack.Api/`): Controllers, OpenIddict endpoints, Razor views for login UI, Scalar API documentation.

### Key Patterns

#### Result Pattern for Domain Operations
All domain and use case operations return `Result<T>` instead of throwing exceptions:
```csharp
var result = User.CreateLocal(email, displayName, passwordHash, dateTimeProvider);
if (result.IsFailure)
    return result.Error;  // DomainError with Code + Description
```

#### Strongly-Typed IDs
All entity IDs use strongly-typed wrappers (e.g., `UserId`, `RoleId`, `GroupId`):
```csharp
public readonly record struct UserId(Guid Value) : IStronglyTypedId<UserId>
```
These auto-convert in EF Core via `ConfigureConventions` in `OpenIdentityStackDbContext`.

#### Domain Errors
Define errors as static `DomainError` fields within domain entities using factory methods:
```csharp
public static readonly DomainError EmailRequired = 
    DomainError.Validation("User.EmailRequired", "Email is required.");
```
Categories: `Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`, `Failure`.

#### Use Case Pattern
Use cases are interfaces defined in Application, implemented in Infrastructure:
```csharp
// Application/Users/Commands/ICreateUserUseCase.cs
public interface ICreateUserUseCase {
    Task<Result<CreateUserResult>> ExecuteAsync(CreateUserCommand command, ...);
}
```
Registration happens in `ServiceCollectionExtensions.AddCommonServices()`.

## Developer Workflow

### Agent workflow

For Spec Kit feature work, prefer the `speckit-phase-implementer` project agent. It should split large phases into subagents or fleet/background sessions when tasks are independent, while keeping final integration and verification in one coordinating session.

### Running the Application
```bash
# Start with Aspire (recommended) - manages PostgreSQL and service discovery
cd src/OpenIdentityStack.AppHost
dotnet run
```
Aspire Dashboard opens automatically. Database migrations apply on startup in Development/Testing.

### Running Tests
```bash
# All tests
dotnet test

# Specific project
dotnet test --project tests/OpenIdentityStack.Domain.Tests/OpenIdentityStack.Domain.Tests.csproj
dotnet test --project tests/OpenIdentityStack.Api.UnitTests/OpenIdentityStack.Api.UnitTests.csproj
```
Tests use xUnit v3 with `[Fact]`/`[Theory]`. API tests use Aspire integration testing via `AppHostFixture`.

### EF Core Migrations
```bash
cd src/OpenIdentityStack.Infrastructure
dotnet ef migrations add <MigrationName> --startup-project ../OpenIdentityStack.Api
dotnet ef database update --startup-project ../OpenIdentityStack.Api
```

## API Structure

- **Admin API** (`/api/admin/*`): CRUD for users, roles, groups, service accounts. Protected by permission-based authorization policies via `.RequireAuthorization(...)`.
- **OIDC Endpoints** (`/connect/*`): Standard OAuth2/OpenIddict endpoints (authorize, token, logout, userinfo).
- **Authentication** (`/Account/*`): Cookie-based login UI with Razor views.

Controllers inject use cases and query handlers directly (not repositories).

## Key Files

| Purpose | Location |
|---------|----------|
| DI registration | `Application/DependencyInjection.cs`, `Infrastructure/ServiceCollectionExtensions.cs` |
| DbContext | `Infrastructure/Persistence/OpenIdentityStackDbContext.cs` |
| Domain base classes | `Domain/Common/` (Entity, AggregateRoot, ValueObject, Result, DomainError) |
| Strongly-typed IDs | `Domain/Common/StronglyTypedIds.cs` |
| Seed data | `Infrastructure/Persistence/SeedData.cs` |
| API test fixture | `tests/OpenIdentityStack.Api.Tests/Fixtures/AppHostFixture.cs` |

## Split-repo rule

Do not add Traceable Isotopes sample projects to this product repository. Samples belong in the OpenIdentityStack samples repository and should consume this product through released containers or public APIs.

## Conventions

- **Naming**: Use cases named `I{Action}UseCase`, query handlers `I{Name}QueryHandler`
- **Package management**: Central Package Management in `Directory.Packages.props`
- **Nullable**: Enabled globally, `TreatWarningsAsErrors` is on
- **Testing**: Shouldly for assertions, NSubstitute for mocks
- **Test seeding**: Use `/api/test-seed/*` endpoints in integration tests (closed-box testing)
