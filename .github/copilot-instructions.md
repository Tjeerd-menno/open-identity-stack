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

#### OpenIddict Projection
Domain `Application` aggregates are projected into OpenIddict's store. Two recurring gotchas:
- **Product naming vs protocol naming:** use `ApplicationProfile`/`profile` everywhere product-facing (Domain, Application, API, contracts, persistence, AdminWeb, docs). The name `ApplicationType` is retained **only** at the OpenIddict adapter/protocol boundary. Do not reintroduce `ApplicationType`/`type` into product-facing code.
- **Confidential apps need a secret:** OpenIddict rejects confidential applications that have no `ClientSecret`, surfacing as `Application.ProjectionFailed`. Ensure a credential exists (or the app is public/disabled) before projecting.

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

> ⚠️ **Do not run the whole solution at once.** `dotnet test` over the full solution (and the full AdminWeb Vitest run) has repeatedly *hung* and had to be killed. Always scope test runs to a single project/module.

```bash
# A single focused project (preferred for the dev loop)
dotnet test --project tests/OpenIdentityStack.Domain.Tests/OpenIdentityStack.Domain.Tests.csproj
dotnet test --project tests/OpenIdentityStack.Api.UnitTests/OpenIdentityStack.Api.UnitTests.csproj

# Run a single test / namespace (Microsoft.Testing.Platform syntax)
dotnet test --project tests/OpenIdentityStack.Domain.Tests/... -- --filter-method "*CreateApplication*"
dotnet test --project tests/OpenIdentityStack.Domain.Tests/... -- --filter-namespace "*Applications*"

# CI-style sequential run for API/integration modules (build first so the DLLs exist)
dotnet build OpenIdentityStack.slnx --no-restore
dotnet test --test-modules "tests/**/bin/Debug/net10.0/*Api.Tests.dll" --max-parallel-test-modules 1 --no-build --no-restore
```
Tests use Microsoft.Testing.Platform with xUnit v3 (`[Fact]`/`[Theory]`). API tests use Aspire integration testing via `AppHostFixture`.

**API integration-test infrastructure gotcha:** `AppHostFixture` uses a shared SQLite test database. It must be seeded **before** the API host starts, otherwise startup services hit `DataProtectionKeys`/`upstream_providers` before the schema is created and the suite hangs. If you see bootstrap hangs, check seed ordering in `AppHostFixture` rather than the test body.

### EF Core Migrations
```bash
cd src/OpenIdentityStack.Infrastructure
dotnet ef migrations add <MigrationName> --startup-project ../OpenIdentityStack.Api
dotnet ef database update --startup-project ../OpenIdentityStack.Api
```

> ⚠️ **After any change to an entity or the `DbContext`, add a migration before running AppHost.** Otherwise the `openidentitystack-db-migrator` resource fails on startup with `PendingModelChangesWarning`, and because `api` waits for the migrator to exit successfully, the whole Aspire stack appears broken even though the real cause is a missing migration.

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
