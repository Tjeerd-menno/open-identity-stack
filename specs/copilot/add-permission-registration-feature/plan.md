# Implementation Plan: Service/API Permission Registry

**Branch**: `copilot/add-permission-registration-feature` | **Date**: 2026-04-28 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/home/runner/work/open-identity-stack/open-identity-stack/specs/copilot/add-permission-registration-feature/spec.md`

**Note**: This workflow stops after Phase 2 planning. It creates planning/design artifacts only and does not implement application code.

## Summary

Replace hard-coded service permission definitions with a persisted, auditable Service/API Permission Registry. The implementation approach uses the existing OpenIdentityStack Clean Architecture solution (`Domain → Application → Infrastructure → Api`) and keeps the feature cohesive as a vertical service-permission slice across those projects. Domain aggregates will model registered services, service permissions, ownership/delegation, lifecycle transitions, dependency safeguards, and audit events. Application use cases will coordinate registration, permission maintenance, authorization, dependency lookup, RBAC catalog queries, and validation. Infrastructure will persist registry data in PostgreSQL through EF Core/Npgsql and read existing role-permission dependencies. Minimal API endpoints under `/api/admin/service-permissions` will expose registration, catalog, lifecycle, dependency, and ownership workflows secured by OpenIddict bearer tokens and RBAC permissions.

## Technical Context

**Language/Version**: C# latest on .NET 10 (`net10.0`, nullable enabled, warnings as errors)  
**Primary Dependencies**: ASP.NET Core Minimal APIs, OpenIddict 7.5.0, EF Core 10.x, Npgsql.EntityFrameworkCore.PostgreSQL 10.x, .NET Aspire 13.3.x, Scalar OpenAPI, xUnit v3, Shouldly, NSubstitute, PactNet  
**Storage**: PostgreSQL via EF Core/Npgsql in `OpenIdentityStackDbContext`; existing in-memory/SQLite test providers may continue where already used  
**Testing**: TDD with xUnit v3; domain unit tests, application use-case tests with NSubstitute, infrastructure integration tests, API integration tests, and OpenAPI/Pact contract tests  
**Target Platform**: Linux-hosted ASP.NET Core API orchestrated by .NET Aspire with PostgreSQL and OpenIddict-backed OAuth2/OIDC  
**Project Type**: Clean Architecture web/API service with feature slices across `OpenIdentityStack.Domain`, `OpenIdentityStack.Application`, `OpenIdentityStack.Infrastructure`, `OpenIdentityStack.Api`, and matching test projects  
**Performance Goals**: Meet constitution targets (P50 ≤100ms, P95 ≤250ms, P99 ≤500ms) for registry list/search/catalog endpoints; support services with at least 100 permissions; allow administrators to locate impacted roles for status changes within 1 minute  
**Constraints**: Preserve stable service identifiers and permission keys; reject invalid updates atomically; enforce owner/delegated-maintainer/admin authorization; never silently delete active-use permissions; audit accepted, denied, validation-failed, and conflicting attempts; avoid sensitive error disclosure  
**Scale/Scope**: Multi-service registry, each service up to at least 100 permissions initially, existing RBAC roles storing string permissions, lifecycle states `active`, `deprecated`, `disabled`, `retired`, and historical visibility for assigned/audited permissions

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate Decision | Evidence in Plan |
|-----------|---------------|------------------|
| I. Test-First Development | PASS | Future Phase 2 tasks must start with failing domain, application, infrastructure, API, and contract tests before implementation. |
| II. Clean Code Standards | PASS | Plan uses explicit aggregates, value objects, use cases, repositories, Result-based domain errors, named lifecycle values, and no speculative dependencies. |
| III. Vertical Slice Architecture | PASS | Feature is a cohesive service-permission-registry slice across the existing Clean Architecture layers with explicit interfaces and encapsulated persistence. |
| IV. Security by Design | PASS | OpenIddict authentication, RBAC permissions, ownership/delegation checks, reserved namespace validation, safe errors, and audit events are planned. |
| V. User Experience Consistency | PASS | API responses and catalog design provide searchable/filterable lists, actionable validation errors, dependency indicators, and consistent lifecycle terminology. |
| VI. Performance Requirements | PASS | Pagination, indexes, dependency-query strategy, and latency targets are included; performance checks are planned. |

No constitution violations or unjustified gate failures are present.

## Project Structure

### Documentation (this feature)

```text
/home/runner/work/open-identity-stack/open-identity-stack/specs/copilot/add-permission-registration-feature/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── service-permission-registry.openapi.yaml
└── tasks.md             # Phase 2 output from /speckit.tasks; not created by this workflow
```

### Source Code (repository root)

```text
/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Domain/
└── ServicePermissions/
    ├── RegisteredService.cs
    ├── ServicePermission.cs
    ├── ServiceOwner.cs
    ├── DelegatedMaintainer.cs
    ├── ServiceLifecycleStatus.cs
    ├── PermissionLifecycleStatus.cs
    ├── RoleAssignmentDependency.cs
    └── ServicePermissionAuditEvent.cs

/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/
├── ServicePermissions/
│   ├── Commands/
│   ├── Queries/
│   ├── Authorization/
│   ├── Validators/
│   └── Dtos/
└── Abstractions/
    ├── IServicePermissionRegistryRepository.cs
    ├── IServicePermissionAuthorizationService.cs
    └── IRolePermissionDependencyReader.cs

/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/
├── Persistence/ServicePermissions/
├── Persistence/Migrations/
└── ServicePermissions/

/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/
└── Admin/
    ├── ServicePermissionsApi.cs
    ├── Requests/
    └── Responses/

/home/runner/work/open-identity-stack/open-identity-stack/tests/
├── OpenIdentityStack.Domain.Tests/ServicePermissions/
├── OpenIdentityStack.Application.Tests/ServicePermissions/
├── OpenIdentityStack.Infrastructure.Tests/ServicePermissions/
├── OpenIdentityStack.Api.Tests/Admin/ServicePermissions/
└── OpenIdentityStack.Contract.Tests/ServicePermissions/
```

**Structure Decision**: Use the repository's established Clean Architecture projects and add a ServicePermissions vertical slice to each layer. This satisfies the constitution's vertical-slice requirement without adding projects or violating dependency direction: Domain has no external dependencies, Application defines use cases and abstractions, Infrastructure implements persistence/dependency readers, and Api exposes secured admin endpoints.

## Phase 0: Outline & Research

Research is complete in [research.md](./research.md). All technical unknowns were resolved:

- Persist registered services and permissions as first-class PostgreSQL entities instead of extending the static `Permissions` class.
- Keep stable permission keys as strings for RBAC compatibility while validating new assignments against the registry.
- Use lifecycle transitions instead of hard deletion for assigned/audited permissions.
- Enforce service owner, delegated maintainer, and administrator override authorization in application/API boundaries.
- Record audit events for accepted, denied, validation-failed, lifecycle, and ownership outcomes.
- Keep Clean Architecture dependency direction while planning feature tasks as a vertical slice.

## Phase 1: Design & Contracts

Design artifacts are complete:

- [data-model.md](./data-model.md): entities, relationships, validation rules, lifecycle transitions, persistence/indexing notes, and RBAC integration rules.
- [contracts/service-permission-registry.openapi.yaml](./contracts/service-permission-registry.openapi.yaml): REST/OpenAPI 3.1 contract for registration, service listing, permission updates, lifecycle changes, dependency lookup, ownership transfer, and RBAC catalog consumption.
- [quickstart.md](./quickstart.md): future TDD, verification, Aspire, security, data, contract, and performance guidance.
- Agent context update requested with `.specify/scripts/powershell/update-agent-context.ps1 -AgentType copilot`.

### Post-Design Constitution Re-check

| Principle | Result | Design Evidence |
|-----------|--------|-----------------|
| I. Test-First Development | PASS | Quickstart requires failing tests first; OpenAPI contract supports contract tests before endpoint implementation. |
| II. Clean Code Standards | PASS | Data model separates aggregate responsibilities and validation rules; API contract uses consistent DTO and problem response shapes. |
| III. Vertical Slice Architecture | PASS | Structure keeps feature concerns explicit while preserving Clean Architecture dependency flow. |
| IV. Security by Design | PASS | Contract includes OAuth2 bearer security; model includes ownership, delegated maintainers, reserved namespace validation, dependency checks, and audit events. |
| V. User Experience Consistency | PASS | Contract/model expose filterable catalogs, dependency details, status indicators, and actionable validation errors. |
| VI. Performance Requirements | PASS | Data model defines indexes and pagination; quickstart includes performance validation for list/catalog/dependency queries. |

No unresolved clarifications remain. No constitution gate failures require justification.

## Complexity Tracking

No constitution violations or complexity exceptions are introduced.
