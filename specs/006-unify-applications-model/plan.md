# Implementation Plan: Unify Applications Domain

**Branch**: `[006-specify-feature]` | **Date**: 2026-05-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-unify-applications-model/spec.md` plus detailed design notes from `/specs/006-unify-applications-model/design.md`

## Summary

Replace the split `Client` and `ServiceAccount` product/domain concepts with one administrator-managed `Application` aggregate for OAuth/OIDC software registrations. The implementation keeps protocol vocabulary such as `client_id` and client authentication where required, but exposes "Application" and "Machine-to-machine application" as the product language across domain, admin APIs, persistence, OpenIddict projection, permissions, audit events, and AdminWeb.

The technical approach is a staged vertical migration: add the new `Applications` domain slice and persistence tables, migrate supported existing `Clients`/`ServiceAccounts`/credentials/certificates into the new model with preflight safeguards, switch OpenIddict synchronization and client authentication to the application repository, introduce `/api/admin/applications`, remove legacy client/service-account admin endpoints as a pre-1.0 breaking change, add an application-profile policy layer so the API enforces valid OAuth/security combinations while AdminWeb railroads administrators through sensible choices, and then complete a terminology refactor from `ApplicationProfile`/`type` to `ApplicationProfile`/`profile` across product-facing surfaces.

## Technical Context

**Language/Version**: .NET 10 / C# latest for backend; TypeScript for AdminWeb.

**Primary Dependencies**: OpenIddict, ASP.NET Core Minimal APIs, EF Core, PostgreSQL, .NET Aspire, React, Vite, Vitest, Playwright. No new backend or frontend package dependencies are planned.

**Storage**: PostgreSQL via EF Core for production and the repository's test database providers for tests. New domain tables: `Applications` and `ApplicationCredentials`; existing `Clients`, `ServiceAccounts`, `ClientCredentials`, and `ClientCertificates` are migration sources only and are removed after the breaking migration path is complete.

**Testing**: Microsoft.Testing.Platform/xUnit-style .NET tests, API/integration tests, contract tests, AdminWeb Vitest tests, and AdminWeb Playwright/E2E coverage where UI flows change.

**Target Platform**: Linux/containerized backend, Windows service package, browser AdminWeb, and Aspire local orchestration.

**Project Type**: Identity/API backend with AdminWeb frontend, DbMigrator/migrations, OpenIddict infrastructure projection, and product/operator documentation.

**Performance Goals**: Application list/detail admin workflows should remain comparable to existing client/service-account workflows; migration preflight should complete before mutation; token issuance should not require duplicated lookups beyond one application/credential validation path; credential validation must avoid logging secrets and must remain suitable for token endpoint traffic.

**Constraints**: Preserve Clean Architecture dependency direction; warnings-as-errors and nullable compliance; direct use cases/query handlers instead of MediatR; explicit mapping instead of AutoMapper; System.Text.Json/Microsoft OpenAPI/Scalar instead of banned packages; plain secrets must never be stored or logged; OpenIddict is a projection, not domain source of truth.

**Scale/Scope**: Covers admin creation/list/get/update/delete/enable/disable, OAuth configuration, credential/certificate lifecycle, machine-to-machine behavior, application-profile policy enforcement for Web, Single Page, Native, Machine-to-machine, and reserved Device profiles, migration from existing client/service-account data where supported, permissions/seeding, audit events, removal of old admin endpoints, AdminWeb navigation/create/detail/credentials UX, terminology alignment to `ApplicationProfile`/`profile`, and docs/deployment guidance. Excludes compatibility endpoints, Dynamic Client Registration, SAML applications, SCIM applications, delegated tenant service principals, implementation of advanced policy options such as private-key JWT/mTLS/DPoP/token lifetime overrides, and unrelated user/role/group/session/federation rewrites.

**Security Impact**: High. This feature changes privileged admin resources, permission names, OAuth client authentication, secrets/certificates, token issuance for disabled applications, migration of security-sensitive data, audit-event vocabulary, and API enforcement of profile-specific OAuth rules that prevent insecure grant/client-type/credential combinations.

**Operational Impact**: High. Requires schema migration, migration preflight reporting, OpenIddict projection synchronization, removal of old route mappings, docs updates, and rollback/remediation guidance focused on database/application deployment rather than legacy endpoint compatibility.

**Documentation Impact**: Product docs and migration/operations docs must explain the new "Application" and "Application Profile" terminology, machine-to-machine replacement for service accounts, profile-specific configuration choices, removed legacy admin endpoints, secret one-time display, and migration failure/remediation cases.

**Package/Dependency Changes**: N/A. Use existing framework and repository patterns.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Status | Evidence / Notes |
|------|--------|------------------|
| I. Security by Design | PASS | Security impact is explicit: unified permissions, one-time secret display, hashed secrets only, certificate credential lifecycle, disabled-app token rejection, audit events, safe migration preflight, and non-sensitive validation errors. |
| II. Test-First, Risk-Based Verification | PASS | Plan requires domain/application tests before implementation, API/integration/contract tests for externally visible behavior, migration tests for data safety, and AdminWeb tests for user-facing changes. |
| III. Layered Architecture with Vertical Feature Slices | PASS | New `Applications` slice flows Domain -> Application -> Infrastructure -> Api/AdminWeb. Domain owns rules; Application owns use cases/ports; Infrastructure owns EF/OpenIddict/audit adapters; Api/AdminWeb are delivery adapters. |
| IV. Simplicity and Dependency Discipline | PASS | Reuses Result pattern, direct use cases, repository/query handlers, explicit DTO mapping, existing OpenIddict/EF/AdminWeb stack, and no new packages. |
| V. Operational Reliability and Observability | PASS | Includes transactional migration where supported, preflight duplicate detection, projection synchronization, logging/audit requirements, old-route removal, and rollback/remediation planning. |
| VI. User-Facing and API Consistency | PASS | Adds `/api/admin/applications` with consistent admin resource shape, pagination/filtering, validation errors, OpenAPI/Scalar metadata, and application-centric AdminWeb terminology. |
| Technology and Package Constraints | PASS | Fits .NET 10/AdminWeb stack, central package management, analyzers, nullable, and banned-package constraints. |
| Documentation Impact | PASS | Requires docs updates for terminology, migration, removed endpoints, credentials, and AdminWeb screenshots. |
| Validation Commands | PASS | Exact commands are listed in quickstart and include restore/build, focused backend tests, API/contract tests, AdminWeb build/lint/test, E2E where UI is changed, and docs strict build. |

## Project Structure

### Documentation (this feature)

```text
specs/006-unify-applications-model/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── design.md
├── contracts/
│   └── applications.openapi.yaml
└── tasks.md             # Created later by /speckit-tasks, not by this command
```

### Source Code (repository root)

```text
src/
├── OpenIdentityStack.Domain/
│   ├── Applications/
│   │   ├── Application.cs
│   │   ├── ApplicationCredential.cs
│   │   ├── ApplicationDomainEvents.cs
│   │   ├── ApplicationErrors.cs
│   │   ├── ApplicationId.cs
│   │   ├── ApplicationProfilePolicy.cs
│   │   ├── ApplicationProfile.cs
│   │   ├── ApplicationStatus.cs
│   │   ├── ApplicationCredentialType.cs
│   │   └── OAuthGrantType.cs
│   ├── Clients/                  # Deprecated/replaced by Applications
│   └── ServiceAccounts/          # Deprecated/replaced by Applications
├── OpenIdentityStack.Application/
│   ├── Applications/
│   │   ├── IApplicationRepository.cs
│   │   ├── Commands/
│   │   └── Queries/
│   ├── Abstractions/
│   │   └── IApplicationProtocolProjection.cs
│   └── Authorization/
│       └── Permissions.cs
├── OpenIdentityStack.Infrastructure/
│   ├── Applications/
│   ├── Identity/
│   │   ├── OpenIddictApplicationProjection.cs
│   │   └── ApplicationClientAuthenticationHandler.cs
│   └── Persistence/
│       ├── Applications/
│       ├── OpenIdentityStackDbContext.cs
│       └── Migrations/
├── OpenIdentityStack.Api/
│   ├── Applications/
│   │   ├── ApplicationsApi.cs
│   │   ├── ApplicationPoliciesApi.cs
│   │   └── ApplicationRequests.cs
│   ├── Clients/                  # Removed in the breaking Applications cleanup
│   └── ServiceAccounts/          # Removed in the breaking Applications cleanup
├── OpenIdentityStack.DbMigrator/
└── OpenIdentityStack.AppHost/

src/OpenIdentityStack.AdminWeb/
├── src/features/applications/
├── src/components/
├── src/lib/
└── src/routes/

tests/
├── OpenIdentityStack.Domain.Tests/
│   └── Applications/
├── OpenIdentityStack.Application.Tests/
│   └── Applications/
├── OpenIdentityStack.Infrastructure.Tests/
│   ├── Applications/
│   ├── Identity/
│   └── Persistence/
├── OpenIdentityStack.Api.Tests/
│   └── Applications/
├── OpenIdentityStack.Contract.Tests/
└── OpenIdentityStack.AdminWeb.E2ETests/

docs/
deploy/
```

**Structure Decision**: Implement a new `Applications` vertical slice and remove `Clients` and `ServiceAccounts` API routes in the same pre-1.0 breaking change. Do not keep `ServiceAccount` as a top-level domain aggregate; migrate supported behavior into `Application` with `ApplicationProfile = MachineToMachine`.

## Complexity Tracking

No constitution violations are required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | N/A | N/A |

## Phase 0 Research Summary

All technical context unknowns are resolved in [research.md](./research.md). Key decisions:

- Use `Application` as the aggregate root and keep `client_id` as the stable protocol identifier.
- Represent service accounts as `ApplicationProfile.MachineToMachine` rather than a separate aggregate.
- Keep domain-owned credential hashes and generalize client authentication from service-account-only validation to application credential validation.
- Remove deprecated `/api/admin/clients` and `/api/admin/service-accounts` endpoints now; no compatibility adapter is a goal.
- Fail production migration preflight when duplicate `client_id` values or invalid service-account grants are found before mutating data.
- Model application-profile policy from `application-type-options-matrix.md` as API-owned business rules; AdminWeb may guide choices but cannot be the source of truth.
- Apply the terminology addendum by exposing the product classification as `ApplicationProfile` in code and `profile` in API/AdminWeb contracts while keeping OpenIddict protocol `ApplicationType` naming inside adapters only.

## Phase 1 Design Summary

Design artifacts are generated:

- [data-model.md](./data-model.md): application entities, fields, relationships, validation rules, state transitions, permission migration, and migration mappings.
- [contracts/applications.openapi.yaml](./contracts/applications.openapi.yaml): unified admin API contract for supported application endpoints.
- [quickstart.md](./quickstart.md): validation and rollout commands.

### Implementation Sequencing

1. **Test-first domain slice**: Add failing tests for application creation, profile invariants, redirect/PKCE rules, credential lifecycle, and disabled lifecycle behavior.
2. **Domain model**: Create `OpenIdentityStack.Domain.Applications` with `Application`, `ApplicationCredential`, strongly typed ID, enums, domain errors, and domain events.
3. **Application layer**: Add repository, commands, query handlers, validation use cases, and `IApplicationProtocolProjection` port.
4. **Persistence**: Add EF configuration, DbSets, repository implementation, migration preflight, new tables, and backfill logic.
5. **OpenIddict projection**: Replace client/service-account-specific registrar behavior with `OpenIddictApplicationProjection`; map grants/endpoints/scopes/PKCE/consent/status from `Application`.
6. **Credential validation**: Replace `ServiceAccountValidationHandler` with `ApplicationClientAuthenticationHandler` that validates active non-revoked/non-expired secrets and certificates for all confidential applications.
7. **Admin API**: Add `/api/admin/applications` endpoints with unified permissions, DTOs, Problem Details-style validation behavior, OpenAPI/Scalar metadata, and contract tests.
8. **Application profile policy**: Add a policy model/service and optional policy endpoint that expose hidden/read-only/available/advanced option availability, default client profile, allowed/default grants, and API validation rules for each application profile. Advanced options remain metadata only until separate protocol support is implemented.
9. **Legacy endpoint removal**: Remove client/service-account API route mappings, compatibility configuration, deprecation metadata, and tests that assert adapter behavior.
10. **Permissions and seed data**: Add `Permissions.Applications.*`, map old permissions, update seed/admin roles, and verify no over/under-granting.
11. **AdminWeb**: Replace separate navigation with Applications, add list filters, profile-first creation/configuration flows, detail tabs, credential/certificate management with one-time secret display, and profile-specific railroaded controls derived from application policy.
12. **Documentation/deployment**: Document terminology, migration, profile policy, removed endpoints, rollback/remediation, and screenshot changed AdminWeb flows.
13. **Terminology refactor**: Rename product-facing `ApplicationProfile`/`type` surfaces to `ApplicationProfile`/`profile` across domain/application profiles, API DTOs and query parameters, AdminWeb models/forms, OpenAPI, docs, and migrations without changing behavior; preserve OpenIddict protocol `ApplicationProfile` naming only where it refers to OpenIddict metadata.

### Design Re-check Constitution Gate

| Gate | Status | Evidence / Notes |
|------|--------|------------------|
| I. Security by Design | PASS | Data model includes credential metadata, secret/certificate lifecycle, permissions, audit events, safe migration failure modes, API-owned application-profile policy enforcement, and clear separation between product profile naming and OpenIddict protocol terminology. |
| II. Test-First, Risk-Based Verification | PASS | Design maps requirements to specific domain/application/API/integration/contract/migration/AdminWeb test categories before implementation. |
| III. Layered Architecture with Vertical Feature Slices | PASS | Contracts and data model preserve Domain/Application/Infrastructure/Api/AdminWeb boundaries. |
| IV. Simplicity and Dependency Discipline | PASS | Design reuses existing patterns and requires no new packages. |
| V. Operational Reliability and Observability | PASS | Migration rollout, preflight, removed-route behavior, audit, and docs are explicit. |
| VI. User-Facing and API Consistency | PASS | Contract defines consistent unified endpoints, pagination/filtering, credential response shapes, `profile` naming for product-facing contracts, and policy-driven option availability for AdminWeb. |
| Technology and Package Constraints | PASS | Design remains within existing .NET/AdminWeb technologies and package constraints. |
| Documentation Impact | PASS | Quickstart and plan require product/operator docs plus AdminWeb screenshots. |
| Validation Commands | PASS | Quickstart lists exact commands for restore/build/tests/docs and targeted suites. |
