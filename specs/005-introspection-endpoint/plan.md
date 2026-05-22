# Implementation Plan: OIDC Token Introspection Endpoint

**Branch**: `005-introspection-endpoint` | **Date**: 2026-05-22 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/005-introspection-endpoint/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Expose OAuth 2.0 token introspection at `/connect/introspect` so authenticated API callers can validate access tokens and receive only the fine-grained permissions relevant to their own service boundary. The implementation should use OpenIddict's built-in introspection pipeline for caller authentication and token activity checks, then enrich successful responses with current, caller-filtered authorization metadata resolved through existing role/query patterns.

## Technical Context

**Language/Version**: C# / .NET 10 backend

**Primary Dependencies**: OpenIddict 7.5.0, ASP.NET Core 10, EF Core 10, Microsoft.Testing.Platform, xUnit v3, NSubstitute, Shouldly

**Storage**: Existing PostgreSQL/EF Core persistence for users, roles, OpenIddict applications, tokens, and authorization entries; no new tables

**Testing**: Microsoft.Testing.Platform with xUnit-style tests; focused API/controller tests, route tests, and infrastructure handler tests

**Target Platform**: Existing API backend for Linux/container and Windows service deployments

**Project Type**: Identity/API backend feature; no AdminWeb UI

**Performance Goals**: Introspection performs one token validation through OpenIddict plus bounded permission lookup/filtering; caller-side caching remains suitable for 30-120 seconds

**Constraints**: Authenticate introspection callers, avoid permission disclosure across service boundaries, preserve warnings-as-errors/analyzer compliance, no banned packages, no token bloat

**Scale/Scope**: One OAuth endpoint, current-user role permission resolution, caller-service permission filtering, test seeder support for introspection-capable clients

**Security Impact**: High. The endpoint handles token metadata and authorization data; unauthenticated or invalid callers must receive no subject or permission metadata. Permissions must be filtered to the authenticated requesting API.

**Operational Impact**: Add request rate limiting for introspection. No migration, certificate, Aspire, deployment, or reverse-proxy change expected.

**Documentation Impact**: Specs only for this feature. No product docs change required unless the public introspection behavior is later documented for API consumers.

**Package/Dependency Changes**: N/A

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Status | Evidence / Notes |
|------|--------|------------------|
| I. Security by Design | PASS | Spec defines caller authentication, token metadata boundaries, permission filtering, rate limiting, and safe failure modes. No secrets are stored or logged. |
| II. Test-First, Risk-Based Verification | PASS | Tests are planned for route mapping, unauthenticated rejection, active response shape, fresh role resolution, token-claim fallback, and caller filtering. |
| III. Layered Architecture with Vertical Feature Slices | PASS | OpenIddict pipeline extension stays in Infrastructure; HTTP/controller surface stays in Api; role lookup uses Application query interface; Domain remains adapter-free. |
| IV. Simplicity and Dependency Discipline | PASS | Uses existing OpenIddict, ASP.NET Core, direct query injection, explicit mapping/filtering, and System.Text.Json-compatible response shapes. No new packages. |
| V. Operational Reliability and Observability | PASS | Adds bounded rate limiting; relies on existing OpenIddict token storage/validation and existing logging/monitoring paths. No migration or deployment change. |
| VI. User-Facing and API Consistency | PASS | Endpoint follows OAuth 2.0 introspection conventions and existing `/connect/*` route style. No AdminWeb UI impact. |
| Technology and Package Constraints | PASS | .NET 10, OpenIddict 7.5.0, central packages, analyzers, and warnings-as-errors are preserved. |
| Documentation Impact | PASS | Feature docs live under `specs/005-introspection-endpoint`; no docs/deploy update required by scope. |
| Validation Commands | PASS | Plan lists focused test commands and full solution build. |

## Project Structure

### Documentation (this feature)

```text
specs/005-introspection-endpoint/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── introspection.openapi.yaml
└── tasks.md
```

### Source Code (repository root)

```text
src/
├── OpenIdentityStack.Api/
│   ├── Authentication/
│   │   └── AuthorizationController.cs
│   └── Program.cs
└── OpenIdentityStack.Infrastructure/
    └── Identity/
        ├── OpenIddictSetup.cs
        └── IntrospectionPermissionsHandler.cs

tests/
├── OpenIdentityStack.Api.UnitTests/
│   └── Endpoints/
│       └── OidcControllerRouteTests.cs
├── OpenIdentityStack.Api.Tests/
│   └── Authentication/
│       └── AuthorizationControllerTests.cs
├── OpenIdentityStack.Infrastructure.Tests/
│   └── Identity/
│       └── IntrospectionPermissionsHandlerTests.cs
└── TestSeedHelpers/
    └── OpenIdentityStackTestSeeder.cs
```

**Structure Decision**: Keep introspection metadata enrichment in the existing OpenIddict Infrastructure slice while preserving the existing `/connect/*` API route tests and controller test coverage. No Domain, AdminWeb, migration, deployment, or package structure changes are required.

## Complexity Tracking

No constitution violations requiring justification.

## Phase 0: Research Summary

See [research.md](research.md). The main design choice is to keep OpenIddict responsible for authenticated introspection and active token checks, then add a scoped OpenIddict server event handler for response enrichment.

## Phase 1: Design Summary

See [data-model.md](data-model.md), [quickstart.md](quickstart.md), and [contracts/introspection.openapi.yaml](contracts/introspection.openapi.yaml). The response model includes standard introspection active state plus optional `sub` and a caller-filtered `permissions` array.

## Post-Design Constitution Check

| Gate | Status | Evidence / Notes |
|------|--------|------------------|
| I. Security by Design | PASS | Design preserves authenticated caller requirement and filters permissions by requesting API. |
| II. Test-First, Risk-Based Verification | PASS | Tasks must place failing tests before implementation and include exact validation commands. |
| III. Layered Architecture with Vertical Feature Slices | PASS | Api, Infrastructure, Application query dependency, and tests are separated by existing layer boundaries. |
| IV. Simplicity and Dependency Discipline | PASS | No new abstractions beyond a focused OpenIddict handler; no new dependencies. |
| V. Operational Reliability and Observability | PASS | Rate limiting and existing token validation/storage paths cover operational risk for this scope. |
| VI. User-Facing and API Consistency | PASS | OAuth endpoint contract follows existing `/connect` conventions. |
| Technology and Package Constraints | PASS | No banned package or stack changes. |
| Documentation Impact | PASS | Spec Kit docs created; no docs/deploy changes required. |
| Validation Commands | PASS | Focused tests and full solution build are defined in quickstart. |
