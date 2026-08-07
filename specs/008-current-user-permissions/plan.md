# Implementation Plan: Current User Permissions

**Branch**: `008-current-user-permissions` | **Date**: 2026-08-07 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/008-current-user-permissions/spec.md`.

## Summary

Add an authenticated `GET /api/me` endpoint that returns the current user's client-readable identity and effective permission snapshot from the validated access-token principal. Update Management Web to treat access tokens as opaque, fetch permissions from `/api/me`, and refresh them when the access-token value changes.

## Technical Context

**Language/Version**: C# / .NET 10 backend; TypeScript/React/Vite Management Web.

**Primary Dependencies**: ASP.NET Core Minimal APIs, OpenIddict validation, `oidc-client-ts`, shared `@openidentitystack/admin-api-client`, Vitest, Microsoft.Testing.Platform.

**Storage**: None. `/api/me` reads the authenticated `ClaimsPrincipal` only.

**Testing**: API unit/route tests, contract tests, frontend API-client tests, and Management Web auth provider tests.

**Target Platform**: Existing API and Management Web deployments, including Production with encrypted access tokens.

**Security Impact**: Medium-high. The change affects UI authorization state and corrects a Production security/operability regression while preserving server-side authorization.

**Operational Impact**: Positive. Production deployments no longer need Testing environment workarounds or disabled access-token encryption for Management Web usability.

**Package/Dependency Changes**: No new packages expected.

## Constitution Check

| Gate | Status | Evidence / Notes |
|------|--------|------------------|
| I. Security by Design | PASS | Access tokens remain encrypted in Production; UI permissions come from validated API principal; frontend treats tokens as opaque. |
| II. Test-First, Risk-Based Verification | PASS | Tests cover backend extraction, route/auth metadata, API contract, opaque-token frontend behavior, and error handling. |
| III. Layered Architecture with Vertical Feature Slices | PASS | Small vertical slice across Api, shared frontend client, and Management Web auth foundation. |
| IV. Simplicity and Dependency Discipline | PASS | Minimal API endpoint and direct shared client contract; no new packages or banned abstractions. |
| V. Operational Reliability and Observability | PASS | Removes unsafe Production workaround; anomalous missing-subject failures are logged. |
| VI. User-Facing and API Consistency | PASS | Management Web continues permission-based gating and explicit auth failure behavior. |

## Project Structure

### Documentation

```text
specs/008-current-user-permissions/
├── spec.md
├── plan.md
├── tasks.md
└── contracts/
    └── current-user.openapi.yaml

docs/adr/
└── 0004-management-web-opaque-access-token-permissions.md
```

### Source Code

```text
src/
├── OpenIdentityStack.Api/
│   ├── CurrentUser/
│   │   └── CurrentUserApi.cs
│   └── Program.cs
├── frontend-packages/
│   └── admin-api-client/
│       └── src/
└── OpenIdentityStack.ManagementWeb/
    └── src/lib/

tests/
├── OpenIdentityStack.Api.UnitTests/
├── OpenIdentityStack.Api.Tests/
├── OpenIdentityStack.Contract.Tests/
└── OpenIdentityStack.ManagementWeb/
```

**Structure Decision**: Put the endpoint in `OpenIdentityStack.Api/CurrentUser/CurrentUserApi.cs` because it is not a managed user resource and not an OIDC protocol endpoint. Add separate route mapping coverage rather than forcing it into admin permission-policy route tests.

## Implementation Slices

### Slice 1: Backend Current User Endpoint

- Add `GET /api/me` authenticated-only endpoint.
- Map endpoint from `Program.cs`.
- Extract `subject`, `userName`, `displayName`, `email`, and explicit permission claims from `HttpContext.User`.
- Deduplicate permissions case-insensitively and preserve first spelling/order.
- Return `401` for missing subject and log anomalous failure.

### Slice 2: Shared Client Contract

- Add `CurrentUserResponse` and `createCurrentUserContract` to `@openidentitystack/admin-api-client`.
- Add OpenAPI contract artifact and contract tests.
- Export current-user contract from package index.

### Slice 3: Management Web Auth Integration

- Set the access token provider before calling `/api/me`.
- Keep auth loading active until the OIDC user and current-user response are resolved.
- Refetch current-user data when the access token value changes.
- Use returned permissions as `auth.permissions`.
- Handle `401` through existing logout/unauthorized behavior.
- Surface non-401 current-user failures explicitly instead of rendering an empty shell.
- Remove Management Web dependency on `extractGrantedPermissions`.

### Slice 4: Documentation

- Add ADR `0004`.
- Update `docs/management-web.md`.
- Add supersession note to `specs/007-management-web/spec.md`.

## Complexity Tracking

No constitution violations. The feature intentionally avoids live permission recalculation, SPA introspection, ID-token permission projection, and access-token encryption changes.

## Post-Design Constitution Check

| Gate | Status | Evidence / Notes |
|------|--------|------------------|
| I. Security by Design | PASS | Design rejects token decoding and preserves encrypted access tokens. |
| II. Test-First, Risk-Based Verification | PASS | Tasks require tests before implementation work in each slice. |
| III. Layered Architecture with Vertical Feature Slices | PASS | Endpoint, client, and UI auth foundation are separated cleanly. |
| IV. Simplicity and Dependency Discipline | PASS | Existing stack only. |
| V. Operational Reliability and Observability | PASS | Production workaround removed; failures become explicit. |
| VI. User-Facing and API Consistency | PASS | UI stays permission-based while backend remains authoritative. |
