# Quickstart: OpenIddict-Based IAM

**Feature**: 001-openiddict-iam  
**Created**: 2026-01-18  
**Updated**: 2026-01-19

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling) (`dotnet workload install aspire`)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for Aspire-managed PostgreSQL)
- IDE: Visual Studio 2024+, VS Code with C# DevKit, or JetBrains Rider

## Quick Setup

### 1. Clone and Build

```bash
git clone <repository-url>
cd open-identity-stack
git checkout 001-openiddict-iam

# Restore and build
dotnet restore
dotnet build
```

### 2. Run with Aspire (Recommended)

```bash
cd src/OpenIdentityStack.AppHost
dotnet run

# Opens Aspire Dashboard automatically at https://localhost:15xxx
# PostgreSQL container managed by Aspire
# API available via service discovery
```

### 3. Alternative: Manual Setup

```bash
# Start PostgreSQL via Docker
docker compose up -d postgres

# Configure development settings
cd src/OpenIdentityStack.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=openidentitystack;Username=postgres;Password=postgres"
dotnet user-secrets set "OpenIddict:EncryptionKey" "<generate-256-bit-key>"
dotnet user-secrets set "OpenIddict:SigningKey" "<generate-256-bit-key>"
```

### 4. Apply Migrations

```bash
cd src/OpenIdentityStack.Infrastructure
dotnet ef database update --startup-project ../OpenIdentityStack.Api
```

### 5. Run the API

```bash
cd src/OpenIdentityStack.Api
dotnet run

# API available at:
# - https://localhost:5001 (HTTPS)
# - http://localhost:5000 (HTTP)
```

### 6. Verify OIDC Endpoints

```bash
# Discovery document
curl https://localhost:5001/.well-known/openid-configuration

# JWKS
curl https://localhost:5001/.well-known/jwks
```

### 7. Access API Reference (Scalar)

Navigate to https://localhost:5001/scalar to view the interactive API documentation.

Scalar provides:
- Beautiful, modern API reference UI
- Interactive request builder
- OAuth2/OIDC authentication testing
- Code samples in multiple languages

---

## Project Structure

```
open-identity-stack/
├── Directory.Build.props           # Common project settings
├── Directory.Packages.props        # Central Package Management
├── global.json                     # SDK version pinning
├── src/
│   ├── OpenIdentityStack.AppHost/       # .NET Aspire orchestrator
│   ├── OpenIdentityStack.ServiceDefaults/ # Shared service configuration
│   ├── OpenIdentityStack.Api/           # HTTP layer, controllers, OpenIddict server
│   ├── OpenIdentityStack.Application/   # Use cases, ports, orchestration
│   ├── OpenIdentityStack.Domain/        # Entities, value objects, domain logic
│   └── OpenIdentityStack.Infrastructure/# EF Core, repositories, adapters
├── tests/
│   ├── OpenIdentityStack.Domain.Tests/
│   ├── OpenIdentityStack.Application.Tests/
│   ├── OpenIdentityStack.Api.Tests/
│   ├── OpenIdentityStack.Infrastructure.Tests/
│   └── OpenIdentityStack.Contract.Tests/
└── specs/                          # Feature specifications
```

---

## Development Workflow

### TDD Cycle (MANDATORY)

1. **Write failing test** in appropriate test project
2. **Run test** to confirm it fails (Red)
3. **Implement minimal code** to make test pass (Green)
4. **Refactor** while keeping tests green
5. **Commit** with descriptive message

### Running Tests

This project uses **Microsoft Testing Platform V2** with xUnit v3. Tests can be run directly as executables or via `dotnet test`.

```bash
# Run all tests (uses Microsoft Testing Platform V2)
dotnet test

# Run specific project directly (recommended - faster)
dotnet run --project tests/OpenIdentityStack.Domain.Tests

# Run with diagnostic output
dotnet run --project tests/OpenIdentityStack.Domain.Tests -- --diagnostic

# List available tests without running
dotnet run --project tests/OpenIdentityStack.Domain.Tests -- --list-tests

# Run with filter
dotnet run --project tests/OpenIdentityStack.Domain.Tests -- --filter "FullyQualifiedName~UserTests"

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Watch mode for TDD
dotnet watch run --project tests/OpenIdentityStack.Domain.Tests
```

### Code Quality

```bash
# Format code
dotnet format

# Analyze code
dotnet build /p:TreatWarningsAsErrors=true
```

---

## Common Tasks

### Create a Local User (via Admin API)

```bash
# First, get an admin token (using seeded admin service account)
TOKEN=$(curl -s -X POST https://localhost:5001/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=admin-cli&client_secret=<secret>&scope=admin" \
  | jq -r '.access_token')

# Create user
curl -X POST https://localhost:5001/api/admin/users \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "displayName": "Test User",
    "password": "SecurePassword123!"
  }'
```

### Test Authorization Code Flow

1. Register a test client in database or via Admin API
2. Open browser to:
   ```
   https://localhost:5001/connect/authorize?
     response_type=code&
     client_id=test-client&
     redirect_uri=https://localhost:5002/callback&
     scope=openid profile&
     code_challenge=<challenge>&
     code_challenge_method=S256
   ```
3. Authenticate as a user
4. Exchange code for tokens

### Test Client Credentials Flow

```bash
curl -X POST https://localhost:5001/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=my-service&client_secret=<secret>&scope=api"
```

---

## Architecture Overview

### Hexagonal Architecture (Ports & Adapters)

```
                    ┌─────────────────────────────┐
                    │         API Layer           │
                    │    (Primary Adapters)       │
                    │  Controllers, Middleware    │
                    └─────────────┬───────────────┘
                                  │
                    ┌─────────────▼───────────────┐
                    │     Application Layer       │
                    │   Use Cases (Commands)      │
                    │   Queries, Port Interfaces  │
                    └─────────────┬───────────────┘
                                  │
                    ┌─────────────▼───────────────┐
                    │       Domain Layer          │
                    │  Entities, Value Objects    │
                    │  Domain Services, Events    │
                    └─────────────────────────────┘
                                  ▲
                    ┌─────────────┴───────────────┐
                    │    Infrastructure Layer     │
                    │   (Secondary Adapters)      │
                    │  Repositories, DB Context   │
                    │  External Service Adapters  │
                    └─────────────────────────────┘
```

### CQRS Pattern

**Commands** (state changes):
```csharp
public record CreateUserCommand(string Email, string DisplayName, string? Password);

public interface ICreateUserUseCase
{
    Task<Result<UserId>> ExecuteAsync(CreateUserCommand command, CancellationToken ct);
}
```

**Queries** (read operations):
```csharp
public record GetUserQuery(UserId Id);

public interface IGetUserQuery
{
    Task<UserDto?> ExecuteAsync(GetUserQuery query, CancellationToken ct);
}
```

---

## Key Technologies

| Component | Technology | Purpose |
|-----------|------------|---------|
| Orchestration | .NET Aspire 13.1 | Local dev orchestration |
| Framework | ASP.NET Core 10 | Web API host |
| OIDC/OAuth2 | OpenIddict 7.2.0 | Token issuance, validation |
| API Docs | Scalar.AspNetCore + Microsoft.AspNetCore.OpenApi | Interactive API reference |
| JSON | System.Text.Json (built-in) | JSON serialization |
| ORM | EF Core 10 | Database access |
| Database | PostgreSQL | Production data store |
| Test Platform | Microsoft Testing Platform V2 | Modern test execution |
| Testing | xunit.v3.mtp-v2, NSubstitute, Shouldly | Unit/integration tests |
| Validation | FluentValidation | Request validation |
| Package Mgmt | Central Package Management | Version control |

### Package Constraints

⚠️ **The following packages are BANNED** - do not add them:

| Package | Reason | Use Instead |
|---------|--------|-------------|
| Newtonsoft.Json | Legacy | System.Text.Json |
| Swashbuckle.* / NSwag.* | Replaced | Scalar.AspNetCore + Microsoft.AspNetCore.OpenApi |
| AutoMapper | Overengineered | Manual mapping / primary constructors |
| MediatR | Unnecessary | Direct use-case injection |

### appsettings.json Structure

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=openidentitystack;..."
  },
  "OpenIddict": {
    "Issuer": "https://localhost:5001",
    "AccessTokenLifetime": "00:15:00",
    "RefreshTokenLifetime": "7.00:00:00"
  },
  "AdminApi": {
    "RequiredScope": "admin"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Environment Variables

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Development, Staging, Production |
| `ConnectionStrings__DefaultConnection` | Database connection string |
| `OpenIddict__EncryptionKey` | Token encryption key (base64) |
| `OpenIddict__SigningKey` | Token signing key (base64) |

---

## Troubleshooting

### Database Connection Issues

```bash
# Verify PostgreSQL is running
docker ps | grep postgres

# Check connection string
dotnet user-secrets list
```

### Token Validation Fails

1. Verify JWKS endpoint is accessible
2. Check token expiration
3. Verify audience/issuer match

### Tests Fail with Database Errors

```bash
# Integration tests use SQLite in-memory by default
# Ensure test projects reference correct configuration

# For Aspire-managed tests, ensure Docker Desktop is running
docker ps
```

---

## Next Steps

1. Review [spec.md](spec.md) for full requirements
2. Review [data-model.md](data-model.md) for entity details
3. Review [contracts/admin-api.yaml](contracts/admin-api.yaml) for API contract
4. Check [tasks.md](tasks.md) for implementation tasks (when available)
