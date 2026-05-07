# Research: OpenIddict-Based IAM

**Feature**: 001-openiddict-iam  
**Created**: 2026-01-18  
**Updated**: 2026-01-19

## Purpose

Document technology decisions, best practices, and alternatives evaluated during planning.

---

## 0. Package Constraints & Disallowed Dependencies

### Decision: Explicit package restrictions for consistency and simplicity

**Disallowed Packages**:

| Package | Status | Reason | Mandated Alternative |
|---------|--------|--------|---------------------|
| `Newtonsoft.Json` | ❌ BANNED | Legacy, unnecessary dependency | `System.Text.Json` (built-in) |
| `Swashbuckle.*` | ❌ BANNED | Use Scalar for API docs | `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` |
| `NSwag.*` | ❌ BANNED | Use Scalar for API docs | `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` |
| `AutoMapper` | ❌ BANNED | Overengineered for this use case | Manual mapping / primary constructors |
| `MediatR` | ❌ BANNED | Adds unnecessary abstraction | Direct service/use-case injection |

**Rationale:**
- **System.Text.Json**: Native .NET JSON serialization, better performance, no external dependency
- **Scalar + Microsoft OpenAPI**: Modern, beautiful API documentation with better DX than Swagger UI
- **Manual Mapping**: C# 12+ primary constructors and records make mapping trivial; AutoMapper adds reflection overhead
- **Direct Injection**: Use-case classes injected directly; MediatR's indirection provides no benefit for this architecture

### JSON Serialization Configuration

```csharp
// Program.cs - Configure System.Text.Json
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// For controller-based APIs
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
```

### API Documentation Setup (Scalar + Microsoft OpenAPI)

```csharp
// Program.cs
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add Microsoft OpenAPI (document generation)
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "OpenIdentityStack Admin API",
            Version = "v1",
            Description = "Administration API for IAM"
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Map OpenAPI document endpoint
app.MapOpenApi();

// Map Scalar API Reference UI (replaces Swagger UI)
app.MapScalarApiReference(options =>
{
    options.WithTitle("OpenIdentityStack API Reference")
           .WithTheme(ScalarTheme.Purple)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
           .AddPreferredSecuritySchemes("OAuth2")
           .AddAuthorizationCodeFlow("OAuth2", flow =>
           {
               flow.ClientId = "scalar-client";
               flow.Pkce = Pkce.Sha256;
           });
});
```

### Manual DTO Mapping Pattern

```csharp
// Instead of AutoMapper, use explicit mapping with primary constructors/records

// Domain Entity
public sealed class User
{
    public UserId Id { get; private set; }
    public string Email { get; private set; }
    public string DisplayName { get; private set; }
    // ...
}

// Response DTO with factory method
public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Status,
    DateTimeOffset CreatedAt)
{
    public static UserResponse FromEntity(User user) => new(
        user.Id.Value,
        user.Email,
        user.DisplayName,
        user.Status.ToString(),
        user.CreatedAt);
}

// Request DTO with ToCommand pattern
public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    string? Password)
{
    public CreateUserCommand ToCommand() => new(Email, DisplayName, Password);
}
```

### Direct Use-Case Injection Pattern

```csharp
// Instead of MediatR, inject use-cases directly

// Use-case interface
public interface ICreateUserUseCase
{
    Task<Result<UserId>> ExecuteAsync(CreateUserCommand command, CancellationToken ct);
}

// Registration
builder.Services.AddScoped<ICreateUserUseCase, CreateUserUseCase>();

// Controller/Endpoint usage
app.MapPost("/api/admin/users", async (
    CreateUserRequest request,
    ICreateUserUseCase useCase,
    CancellationToken ct) =>
{
    var result = await useCase.ExecuteAsync(request.ToCommand(), ct);
    return result.Match(
        success => Results.Created($"/api/admin/users/{success.Value}", null),
        failure => Results.BadRequest(failure.ToProblemDetails()));
});
```

---

## 1. OpenIddict Configuration & Best Practices

### Decision: Use OpenIddict 7.2.0 with Server + Validation packages

**Rationale:**
- OpenIddict is the de facto standard for OIDC/OAuth2 in ASP.NET Core
- Version 7.2.0 is the latest stable release with full .NET 10 support
- Provides both server (token issuance) and validation (token verification) components
- Supports EF Core for application/authorization/scope/token storage
- Includes FAPI 2.0 support and improved performance

**Alternatives Considered:**
- **IdentityServer**: Commercial licensing, overkill for this use case
- **Duende IdentityServer**: License costs for commercial use
- **Custom implementation**: High complexity, security risk

### Key Configuration Patterns

```csharp
// Recommended OpenIddict server setup
services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<OpenIdentityStackDbContext>())
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetTokenEndpointUris("/connect/token")
               .SetUserinfoEndpointUris("/connect/userinfo")
               .SetLogoutEndpointUris("/connect/logout")
               .SetIntrospectionEndpointUris("/connect/introspect")
               .SetRevocationEndpointUris("/connect/revoke");
        
        options.AllowAuthorizationCodeFlow()
               .AllowClientCredentialsFlow()
               .AllowRefreshTokenFlow()
               .RequireProofKeyForCodeExchange();
        
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();
        
        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .EnableLogoutEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });
```

### Best Practices

1. **PKCE Required**: Always require PKCE for authorization code flow (public clients)
2. **Key Rotation**: Implement signing key rotation with overlap period
3. **Token Lifetimes**: Short access tokens (15-60 min), longer refresh tokens with rotation
4. **Scope Validation**: Validate requested scopes against client configuration

---

## 2. Hexagonal Architecture with ASP.NET Core

### Decision: Four-project structure with explicit port/adapter separation

**Rationale:**
- Clear dependency direction: Api → Application → Domain ← Infrastructure
- Domain has zero external dependencies (pure C#)
- Infrastructure implements Application port interfaces
- Enables testing each layer in isolation

**Alternatives Considered:**
- **Clean Architecture (Onion)**: Similar, but Hexagonal more explicit about ports/adapters
- **N-tier**: Weaker boundaries, harder to test
- **Single project with folders**: Dependencies become implicit, harder to enforce

### Dependency Rules

```
OpenIdentityStack.Api
├── References: OpenIdentityStack.Application, OpenIdentityStack.Infrastructure
└── Purpose: HTTP adapters, DI composition root

OpenIdentityStack.Application
├── References: OpenIdentityStack.Domain
└── Purpose: Use cases, port interfaces, orchestration

OpenIdentityStack.Domain
├── References: None (pure domain)
└── Purpose: Entities, value objects, domain services, domain events

OpenIdentityStack.Infrastructure
├── References: OpenIdentityStack.Application, OpenIdentityStack.Domain
└── Purpose: Repository implementations, external service adapters
```

---

## 2a. .NET Aspire 13.1 Integration

### Decision: Use .NET Aspire 13.1 for local development orchestration

**Rationale:**
- Simplifies local development with automatic service discovery
- Manages PostgreSQL container lifecycle during development
- Built-in OpenTelemetry integration for distributed tracing
- Health checks and resilience patterns via ServiceDefaults
- Dashboard for monitoring all services during development

**Alternatives Considered:**
- **Docker Compose**: No .NET integration, manual configuration
- **Kubernetes locally**: Too heavy for development
- **Manual container management**: Error-prone, inconsistent

### AppHost Configuration

```csharp
// OpenIdentityStack.AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume();

var db = postgres.AddDatabase("openidentitystack");

builder.AddProject<Projects.OpenIdentityStack_Api>("api")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
```

### ServiceDefaults Pattern

```csharp
// OpenIdentityStack.ServiceDefaults/Extensions.cs
public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
{
    builder.ConfigureOpenTelemetry();
    builder.AddDefaultHealthChecks();
    builder.Services.AddServiceDiscovery();
    builder.Services.ConfigureHttpClientDefaults(http =>
    {
        http.AddStandardResilienceHandler();
        http.AddServiceDiscovery();
    });
    return builder;
}
```

---

## 2b. Central Package Management & Build Configuration

### Decision: Use Central Package Management with Directory.Build.props

**Rationale:**
- Single source of truth for all package versions
- Consistent versions across all projects
- Easier security updates and dependency management
- Common build settings applied uniformly

### Directory.Packages.props (Central Package Management)

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  
  <ItemGroup>
    <!-- Aspire -->
    <PackageVersion Include="Aspire.Hosting.AppHost" Version="13.1.0" />
    <PackageVersion Include="Aspire.Hosting.PostgreSQL" Version="13.1.0" />
    <PackageVersion Include="Microsoft.Extensions.ServiceDiscovery" Version="13.1.0" />
    
    <!-- ASP.NET Core / EF Core -->
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
    
    <!-- OpenIddict -->
    <PackageVersion Include="OpenIddict" Version="7.2.0" />
    <PackageVersion Include="OpenIddict.AspNetCore" Version="7.2.0" />
    <PackageVersion Include="OpenIddict.EntityFrameworkCore" Version="7.2.0" />
    
    <!-- API Documentation (Scalar - replaces Swagger) -->
    <PackageVersion Include="Scalar.AspNetCore" Version="2.0.0" />
    
    <!-- Validation -->
    <PackageVersion Include="FluentValidation" Version="11.11.0" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.11.0" />
    
    <!-- Testing (Microsoft Testing Platform V2) -->
    <PackageVersion Include="xunit.v3.mtp-v2" Version="1.0.0" />
    <PackageVersion Include="NSubstitute" Version="5.3.0" />
    <PackageVersion Include="Shouldly" Version="4.2.1" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="coverlet.collector" Version="6.0.2" />
  </ItemGroup>
  
  <!-- 
    BANNED PACKAGES - DO NOT ADD:
    - Newtonsoft.Json (use System.Text.Json)
    - Swashbuckle.* (use Scalar.AspNetCore + Microsoft.AspNetCore.OpenApi)
    - NSwag.* (use Scalar.AspNetCore + Microsoft.AspNetCore.OpenApi)
    - AutoMapper (use manual mapping)
    - MediatR (use direct service injection)
  -->
</Project>
```

### Directory.Build.props (Common Settings)

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>13</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>
  
  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <Optimize>true</Optimize>
    <DebugType>portable</DebugType>
  </PropertyGroup>
  
  <ItemGroup>
    <Using Include="System.Threading.Tasks" />
  </ItemGroup>
</Project>
```

### global.json (SDK Version Pinning)

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestMinor"
  }
}
```

---

## 3. CQRS Implementation Pattern

### Decision: Usecase classes for commands, Query classes for reads

**Rationale:**
- Separates read/write concerns for clarity
- Commands can trigger domain events; queries are side-effect free
- Enables different optimization strategies (read replicas, caching)
- MediatR optional but useful for cross-cutting concerns (validation, logging)

**Alternatives Considered:**
- **Simple services mixing commands/queries**: Harder to scale, conflated concerns
- **Event sourcing**: Overkill for this use case, adds complexity

### Pattern Examples

```csharp
// Command with Usecase interface
public record CreateUserCommand(string Email, string DisplayName);

public interface ICreateUserUseCase
{
    Task<Result<UserId>> ExecuteAsync(CreateUserCommand command, CancellationToken ct);
}

public class CreateUserUseCase : ICreateUserUseCase
{
    private readonly IUserRepository _users;
    
    public async Task<Result<UserId>> ExecuteAsync(CreateUserCommand command, CancellationToken ct)
    {
        var user = User.Create(command.Email, command.DisplayName);
        await _users.AddAsync(user, ct);
        return Result.Success(user.Id);
    }
}

// Query with Query interface
public record GetUserQuery(UserId Id);

public interface IGetUserQuery
{
    Task<UserDto?> ExecuteAsync(GetUserQuery query, CancellationToken ct);
}
```

---

## 4. EF Core 10 Best Practices

### Decision: Code-first migrations with explicit configurations

**Rationale:**
- Full control over schema
- Migrations tracked in source control
- Fluent configuration in separate files per entity

**Patterns:**
- Use `IEntityTypeConfiguration<T>` per entity
- Indexes on frequently queried columns (Email, Subject+Issuer)
- Soft deletes for user retention compliance
- Audit columns (CreatedAt, ModifiedAt, CreatedBy)

### Performance Considerations

1. **Compiled Queries**: Use for hot paths (user lookup by ID/email)
2. **Split Queries**: For entities with multiple collections
3. **Projection**: Use `.Select()` for queries, avoid loading full entities
4. **No Tracking**: Use `.AsNoTracking()` for read-only queries

---

## 5. Session Management Strategy

### Decision: Database-backed sessions with distributed cache for hot data

**Rationale:**
- Sessions must survive server restarts
- Revocation requires persistence
- Distributed cache (Redis) for frequently accessed session data

**Alternatives Considered:**
- **In-memory only**: Lost on restart, doesn't scale horizontally
- **JWT-only (stateless)**: Cannot revoke individual sessions

### Implementation Approach

1. Session record in database with status, user, client, timestamps
2. Refresh tokens tied to session ID
3. Session revocation marks record as revoked
4. Refresh token validation checks session status

---

## 6. Single Logout (SLO) Implementation

### Decision: Support both front-channel and back-channel logout

**Rationale:**
- Front-channel: Browser-mediated, works with SPAs
- Back-channel: Server-to-server, more reliable for server apps
- Both are OIDC standard mechanisms

### Implementation Approach

1. Track client sessions per user session
2. On logout, iterate registered clients
3. Front-channel: Render iframes to logout URIs
4. Back-channel: POST logout tokens to client endpoints
5. Fire-and-forget with logging for failures (don't block user logout)

---

## 7. Upstream Federation Strategy

### Decision: ASP.NET Core external authentication with custom handlers

**Rationale:**
- Leverage built-in OIDC authentication handler
- Custom callback to handle JIT provisioning
- Support multiple providers with dynamic configuration

### JIT Provisioning Flow

1. User authenticates at upstream IdP
2. Callback receives claims from upstream
3. Lookup user by (issuer, subject)
4. If not found and JIT enabled: create user record
5. If not found and JIT disabled: reject with error
6. Apply claim mappings and group/role assignments
7. Issue local session and tokens

---

## 8. Admin API Security Model

### Decision: OAuth2 protected with RBAC using roles/permissions

**Rationale:**
- Dogfooding: Admin API protected by the IAM itself
- Roles define permission sets (UserAdmin, RoleAdmin, SuperAdmin)
- Granular permissions checked per endpoint

### Permission Model

```
SuperAdmin
├── users:* (full user management)
├── groups:* (full group management)
├── roles:* (full role management)
├── clients:* (full client management)
├── sessions:* (full session management)
└── providers:* (full provider configuration)

UserAdmin
├── users:read
├── users:create
├── users:update
└── users:disable

RoleAdmin
├── roles:read
├── roles:create
├── roles:update
└── roles:assign
```

---

## 9. Audit Logging Strategy

### Decision: Structured audit events to database with async processing

**Rationale:**
- Compliance requires audit trail of security operations
- Structured events enable querying and analysis
- Async to avoid blocking request processing

### Audit Event Structure

```csharp
public record AuditEvent(
    Guid Id,
    DateTimeOffset Timestamp,
    string Action,           // "user.created", "role.assigned"
    string ActorId,          // Who performed the action
    string ActorType,        // "user", "service_account"
    string ResourceType,     // "user", "group", "role"
    string ResourceId,       // Target resource ID
    JsonDocument? Details,   // Additional context
    string IpAddress,
    string UserAgent
);
```

---

## 10. Testing Strategy

### Decision: Microsoft Testing Platform V2 with xUnit v3, NSubstitute, and Shouldly

**Test Categories:**

| Layer | Type | Tools | Purpose |
|-------|------|-------|---------|
| Domain | Unit | xunit.v3.mtp-v2, Shouldly | Entity behavior, domain logic |
| Application | Unit | xunit.v3.mtp-v2, NSubstitute, Shouldly | Use case logic with mocked ports |
| Infrastructure | Integration | xunit.v3.mtp-v2, Shouldly | Repository against SQLite in-memory |
| API | Integration | xunit.v3.mtp-v2, WebApplicationFactory, Shouldly | Full request/response cycle |
| API | Contract | xunit.v3.mtp-v2, Shouldly | API shape stability |

**Framework Choices:**
- **Microsoft Testing Platform V2**: Modern, lightweight test platform replacing VSTest
- **xUnit v3 (mtp-v2)**: `xunit.v3.mtp-v2` package includes native MTP V2 support
- **Shouldly**: Readable assertion syntax with excellent error messages
- **NSubstitute**: Clean mocking syntax, works well with interfaces

**Alternatives Rejected:**
- **VSTest**: Legacy platform, replaced by Microsoft Testing Platform V2
- **Moq**: Castle.DynamicProxy dependency, license concerns
- **Testcontainers**: SQLite in-memory sufficient for integration tests, avoids Docker dependency
- **FluentAssertions**: Shouldly preferred for simpler API

### Microsoft Testing Platform V2 Configuration

Microsoft Testing Platform V2 is a lightweight, portable alternative to VSTest that is embedded directly in test projects. xUnit v3 has native support for this platform.

**global.json Configuration (Required for .NET 10+):**

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestMinor"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

**Test Project Configuration:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    
    <!-- Microsoft Testing Platform V2 settings -->
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
    <EnableMSTestRunner>false</EnableMSTestRunner>
    <UseVSTest>false</UseVSTest>
  </PropertyGroup>

  <ItemGroup>
    <!-- xUnit v3 with Microsoft Testing Platform V2 (includes all MTP dependencies) -->
    <PackageReference Include="xunit.v3.mtp-v2" />
    
    <!-- Assertions and mocking -->
    <PackageReference Include="Shouldly" />
    <PackageReference Include="NSubstitute" />
    
    <!-- Coverage -->
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>
</Project>
```

**Running Tests with Microsoft Testing Platform V2:**

```bash
# Run tests directly (recommended - uses MTP V2)
dotnet run --project tests/OpenIdentityStack.Domain.Tests

# Run using dotnet test (requires global.json configuration)
dotnet test

# Run specific test project
dotnet test tests/OpenIdentityStack.Domain.Tests

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run with diagnostic output
dotnet run --project tests/OpenIdentityStack.Domain.Tests -- --diagnostic

# List available tests without running
dotnet run --project tests/OpenIdentityStack.Domain.Tests -- --list-tests

# Run with filter
dotnet run --project tests/OpenIdentityStack.Domain.Tests -- --filter "FullyQualifiedName~UserTests"
```

**Benefits of Microsoft Testing Platform V2:**
- **Deterministic**: No reflection-based test discovery at runtime
- **Compile-time registration**: Extensions registered during compilation
- **Zero dependencies**: Core platform is a single assembly
- **Native AOT compatible**: Supports ahead-of-time compilation
- **Hostable**: Can run in any .NET application context
- **Performant**: Lightweight orchestration without runtime overhead

### TDD Workflow

1. Write failing test (Red)
2. Implement minimal code to pass (Green)
3. Refactor while keeping tests green
4. Commit

---

## Summary of Decisions

| Area | Decision | Confidence |
|------|----------|------------|
| OIDC Framework | OpenIddict 7.2.0 | High |
| Orchestration | .NET Aspire 13.1 | High |
| Architecture | Hexagonal (4 projects) | High |
| CQRS | Usecase/Query classes | High |
| ORM | EF Core 10 code-first | High |
| Sessions | DB + distributed cache | High |
| SLO | Front + back channel | High |
| Federation | ASP.NET Core OIDC handlers | High |
| Admin Security | OAuth2 + RBAC | High |
| Audit | Structured events to DB | High |
| Testing | xUnit v3, NSubstitute, Shouldly | High |
