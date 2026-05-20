# Implementation Plan: OpenIddict-Based Identity & Access Management

**Branch**: `001-openiddict-iam` | **Date**: 2026-01-19 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-openiddict-iam/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Build an OpenIddict-based Identity & Access Management (IAM) solution supporting local users, federated users (upstream OIDC), groups, roles, Admin API, service accounts, session management, and Single Logout. Uses Hexagonal architecture with .NET 10, EF Core, and .NET Aspire for orchestration.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0  
**Primary Dependencies**:
- OpenIddict 7.5.0 (OIDC/OAuth2 server)
- ASP.NET Core 10.0
- EF Core 10.0
- .NET Aspire 13.3.x (orchestration)
- Microsoft.AspNetCore.OpenApi (OpenAPI document generation)
- Scalar.AspNetCore (API reference UI - replaces Swagger UI)
- FluentValidation 11.11.0 (input validation)

**Storage**: PostgreSQL (via Aspire, Npgsql.EntityFrameworkCore.PostgreSQL)  
**Testing**: Microsoft Testing Platform V2, xunit.v3.mtp-v2, NSubstitute, Shouldly, Microsoft.AspNetCore.Mvc.Testing  
**Target Platform**: Linux server / Docker containers  
**Project Type**: Web API (Hexagonal/Clean Architecture)  
**Performance Goals**: P95 ≤250ms, 1000 concurrent auth requests  
**Constraints**: P99 ≤500ms, session revocation effective within 5s  
**Scale/Scope**: Enterprise IAM supporting 10k+ users

## Package Constraints (MANDATORY)

The following packages are **explicitly disallowed** and MUST NOT be used:

| Package | Reason | Alternative |
|---------|--------|-------------|
| `Newtonsoft.Json` | Use native JSON | `System.Text.Json` (built-in) |
| `Swashbuckle.*` / Swagger | Use Scalar + Microsoft OpenAPI | `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` |
| `AutoMapper` | Use native C# | Manual mapping / primary constructors |
| `MediatR` | Use native C# | Direct service/use-case injection |

**Allowed package sources**: Official Microsoft packages + carefully selected OSS packages that provide irreplaceable functionality (OpenIddict, FluentValidation, xUnit, NSubstitute, Shouldly).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First Development | ✅ PASS | TDD workflow enforced, xUnit v3 + NSubstitute + Shouldly configured |
| II. Clean Code Standards | ✅ PASS | Single responsibility, small functions, no magic numbers |
| III. Vertical Slice Architecture | ✅ PASS | Feature folders under each layer (Users/, Groups/, Roles/, etc.) |
| IV. Security by Design | ✅ PASS | Input validation (FluentValidation), RBAC, password hashing, no secrets in code |
| V. User Experience Consistency | ✅ PASS | RFC 7807 Problem Details, consistent error responses |
| VI. Performance Requirements | ✅ PASS | P50 ≤100ms, P95 ≤250ms, P99 ≤500ms targets defined |

**Package Constraints Check**:
- ❌ No Newtonsoft.Json → Use `System.Text.Json`
- ❌ No Swagger/Swashbuckle → Use `Scalar.AspNetCore` + `Microsoft.AspNetCore.OpenApi`
- ❌ No AutoMapper → Use manual mapping with primary constructors
- ❌ No MediatR → Use direct use-case/service injection

## Project Structure

### Documentation (this feature)

```text
specs/001-openiddict-iam/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── admin-api.yaml   # OpenAPI 3.1 specification
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── OpenIdentityStack.AppHost/           # .NET Aspire orchestrator
│   ├── AppHost.cs
│   └── appsettings.json
├── OpenIdentityStack.ServiceDefaults/   # Shared service configuration
│   └── Extensions.cs
├── OpenIdentityStack.Api/               # HTTP layer (Primary Adapters)
│   ├── Program.cs                  # Composition root, Scalar + OpenAPI setup
│   ├── Admin/                      # Admin API endpoints
│   │   ├── Users/
│   │   ├── Groups/
│   │   ├── Roles/
│   │   ├── ServiceAccounts/
│   │   ├── Sessions/
│   │   └── Providers/
│   ├── Authentication/             # OIDC/OAuth2 endpoints
│   ├── Authorization/              # Authorization handlers
│   ├── Common/                     # Shared middleware, filters
│   └── Shared/                     # DTOs, contracts
├── OpenIdentityStack.Application/       # Use cases, Ports (Application layer)
│   ├── Abstractions/               # Port interfaces
│   ├── Users/                      # User use cases
│   ├── Groups/                     # Group use cases
│   ├── Roles/                      # Role use cases
│   ├── ServiceAccounts/            # Service account use cases
│   ├── Sessions/                   # Session use cases
│   └── Federation/                 # Federation use cases
├── OpenIdentityStack.Domain/            # Entities, Value Objects (Domain layer)
│   ├── Users/
│   ├── Groups/
│   ├── Roles/
│   ├── ServiceAccounts/
│   ├── Sessions/
│   └── Common/                     # Shared domain primitives
└── OpenIdentityStack.Infrastructure/    # Adapters (Infrastructure layer)
    ├── Persistence/                # EF Core DbContext, repositories
    ├── Identity/                   # OpenIddict integration
    ├── ExternalProviders/          # Upstream IdP handlers
    └── Audit/                      # Audit logging

tests/
├── OpenIdentityStack.Domain.Tests/      # Unit tests for domain logic
├── OpenIdentityStack.Application.Tests/ # Unit tests for use cases
├── OpenIdentityStack.Api.Tests/         # Integration tests for API
├── OpenIdentityStack.Infrastructure.Tests/ # Integration tests for repos
└── OpenIdentityStack.Contract.Tests/    # API contract stability tests
```

**Structure Decision**: Hexagonal Architecture with vertical slices per feature domain. Each layer follows the feature-folder convention (Users/, Groups/, etc.) enabling parallel development and clear boundaries.

## Complexity Tracking

> **No constitution violations requiring justification.**

The architecture follows established patterns:
- 4-project structure (Api, Application, Domain, Infrastructure) is standard for Hexagonal Architecture
- Direct service injection instead of MediatR reduces complexity
- Manual DTO mapping with primary constructors is simpler than AutoMapper
- System.Text.Json is the modern .NET standard for JSON serialization
- Scalar + Microsoft OpenAPI provides better DX than Swagger
