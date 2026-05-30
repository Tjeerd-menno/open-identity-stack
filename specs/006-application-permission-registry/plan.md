# Implementation Plan: Application Permission Registry

**Branch**: `006-application-permission-registry` | **Date**: 2026-05-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/006-application-permission-registry/spec.md` and decisions from [decision-log.md](decision-log.md)

## Summary

Promote application/resource permission registration into the authoritative numbered roadmap. The feature introduces complete versioned permission manifests, current application states, ownership and delegated maintainers, dynamic permission catalogs with derived aggregate wildcards, strict role assignment validation, concrete-only token/introspection permission emission, destructive tombstone workflows with automatic assignment cleanup, trusted remote manifest import, and AdminWeb workflows. Backend and AdminWeb ship together in vertical slices.

## Technical Context

**Language/Version**: C# / .NET 10 backend; TypeScript/React/Vite AdminWeb

**Primary Dependencies**: ASP.NET Core 10 Minimal APIs for admin resource APIs, EF Core 10/PostgreSQL, OpenIddict 7.5.0, Microsoft.Testing.Platform, Vitest, Playwright E2E

**Storage**: EF Core persistence for registered permission applications, permissions, tombstones/deleted rows, maintainers, manifest metadata, audit records, and role assignment updates through existing role persistence

**Testing**: xUnit-style .NET tests through Microsoft.Testing.Platform, contract tests for `/api/admin/*`, Vitest for AdminWeb units, Playwright/AdminWeb E2E for vertical flows

**Target Platform**: Current API/AdminWeb deployment through Aspire/local containers and existing production container model

**Project Type**: Full-stack IAM/admin feature with backend API, persistence, authorization, token/introspection behavior, and AdminWeb workflows

**Performance Goals**: Catalog and assignment validation should use indexed application identifiers/full permission keys; token/introspection expansion should batch registry lookup for assigned dynamic permissions and avoid per-permission queries

**Constraints**: No MediatR, no Swashbuckle, no Native AOT constraints for current implementation, warnings-as-errors, current project layering, clean database allowed because project is pre-1.0 alpha

**Scale/Scope**: Five vertical slices; each application up to at least 100 concrete permissions; role picker and catalog handle concrete and derived wildcard grants; destructive operations update assignments transactionally

**Security Impact**: High. The feature mutates authorization catalogs, role assignments, token permission emission, remote server-side fetches, ownership boundaries, and audit data.

**Operational Impact**: Requires clean database/destructive reset for alpha. Adds remote manifest fetch constraints and audit-heavy registry operations.

**Documentation Impact**: Adds numbered Spec Kit docs and later should retire non-authoritative Copilot application-permission registry specs.

**Package/Dependency Changes**: No new package expected unless SemVer parsing cannot be implemented safely with existing stack. If a package is proposed, it must pass central package and banned-package review.

## Constitution Check

| Gate | Status | Evidence / Notes |
|------|--------|------------------|
| I. Security by Design | PASS | Spec defines ownership/admin boundaries, broad-grant acknowledgement, concrete-only token emission, remote fetch safety, transaction/audit requirements, and clean failure modes. |
| II. Test-First, Risk-Based Verification | PASS | Security-sensitive external behavior requires API/integration tests; project-owned admin APIs require contract tests; AdminWeb flows require focused E2E. |
| III. Layered Architecture with Vertical Feature Slices | PASS | Feature is explicitly sliced end-to-end; Application owns orchestration/ports; Infrastructure owns EF transactions; Api owns Minimal API endpoint mappers; AdminWeb ships with backend slices. |
| IV. Simplicity and Dependency Discipline | PASS | No MediatR/Swashbuckle; direct use-case/query-handler injection; no Native AOT migration forced while EF Core blocks AOT. |
| V. Operational Reliability and Observability | PASS | Destructive changes are previewed in AdminWeb, recalculated by API, transactional, and audited with details. Remote fetches are bounded and trusted. |
| VI. User-Facing and API Consistency | PASS | AdminWeb navigation is `Application Permissions`; role response `permissions: string[]` remains stable; catalogs keep platform and dynamic sources distinct. |
| Technology and Package Constraints | PASS | Uses existing stack and central dependency discipline. |
| Documentation Impact | PASS | Numbered spec replaces non-authoritative Copilot planning text. |
| Validation Commands | PASS | Quickstart defines slice-level verification expectations. |

## Project Structure

### Documentation

```text
specs/006-application-permission-registry/
├── decision-log.md
├── spec.md
├── plan.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── application-permission-registry.openapi.yaml
└── tasks.md
```

### Source Code

```text
src/
├── OpenIdentityStack.Domain/
│   └── ApplicationPermissions/
├── OpenIdentityStack.Application/
│   ├── ApplicationPermissions/
│   ├── Authorization/
│   └── Abstractions/
├── OpenIdentityStack.Infrastructure/
│   ├── Persistence/
│   └── ApplicationPermissions/
├── OpenIdentityStack.Api/
│   ├── Admin/
│   └── Authorization/
└── OpenIdentityStack.AdminWeb/
    └── src/

tests/
├── OpenIdentityStack.Domain.Tests/
├── OpenIdentityStack.Application.Tests/
├── OpenIdentityStack.Infrastructure.Tests/
├── OpenIdentityStack.Api.UnitTests/
├── OpenIdentityStack.Api.Tests/
├── OpenIdentityStack.Contract.Tests/
└── OpenIdentityStack.AdminWeb.E2ETests/
```

**Structure Decision**: Keep registry domain rules in Domain, orchestration and ports in Application, EF repositories/transactions/assignment-store implementations in Infrastructure, Minimal API endpoint mappers in Api, and workflow UI under AdminWeb. AdminWeb and backend are implemented together by vertical slice.

## Implementation Slices

### Slice 1: Application Registration And Inline Manifest Management

- Inline `POST /api/admin/application-permissions/applications` create.
- Target-specific inline manifest preview/apply for non-destructive newer manifests.
- Application list/detail, active/disabled state, manifest version, ownership, maintainers, optional `manifestBaseUrl`.
- User and group owner/maintainer authorization.
- Maintainer add/remove and ownership transfer.
- Enable/disable admin actions without role impact endpoint unless real role impact can be returned.
- AdminWeb `Application Permissions` navigation, list/detail/create/update, raw JSON and structured manifest editor.
- Reject destructive omissions with `409 DestructiveManifestChangeNotSupportedYet`.
- No delete/tombstone/redeclare, remote import, role picker, platform catalog, or multi-segment wildcard matcher change yet.

### Slice 2: Role Picker, Assignment Validation, Broad Grants, And Permission Emission

- Dynamic catalog with concrete and derived aggregate wildcard entries.
- Platform permission catalog endpoint.
- Unified AdminWeb role picker.
- Strict role assignment validation for platform and dynamic permissions.
- Broad-grant acknowledgement for platform wildcards, dynamic aggregate wildcards, and `*`.
- Multi-segment wildcard matcher support.
- Token/introspection concrete-only emission and dynamic registry validation.
- API/integration tests for introspection filtering/expansion; AdminWeb E2E for wildcard assignment flow.

### Slice 3: Destructive Manifest/Delete Workflows

- Destructive manifest omissions.
- Manual permission delete.
- Application delete.
- Permission/application deletion impact preview.
- Tombstones, automatic exact assignment removals, wildcard collapse removals, transaction/audit details.
- AdminWeb destructive preview and confirmation flows.
- One representative destructive E2E flow; API/integration tests for all destructive paths.

### Slice 4: Remote Import And Trust Flow

- Target-specific remote import preview/apply from registered `manifestBaseUrl`.
- Remote fetch security constraints.
- AdminWeb remote import UI.
- E2E using a controlled local fixture endpoint.

### Slice 5: Tombstone History, Replacement Guidance, And Diagnostics

- Explicit history/tombstone reads.
- Removed-permission replacement guidance.
- Integrity diagnostics/remediation surfaces.
- Final planned slice for `006`.

## Complexity Tracking

No constitution violations. The feature is large, so complexity is controlled by vertical slices and by deferring exact DTO finalization to the contract artifact before task generation.

## Phase 0: Research Summary

See [decision-log.md](decision-log.md). Research/grilling resolved the primary semantic risks: permission identity, manifest completeness and versioning, wildcard derivation and expansion, assignment cleanup, transaction/audit boundaries, ownership, remote fetch trust, and AdminWeb slice scope.

## Phase 1: Design Summary

See [data-model.md](data-model.md), [quickstart.md](quickstart.md), and [contracts/application-permission-registry.openapi.yaml](contracts/application-permission-registry.openapi.yaml). The OpenAPI artifact starts as a contract outline and must be made exact before implementation tasks are executed.

## Post-Design Constitution Check

| Gate | Status | Evidence / Notes |
|------|--------|------------------|
| I. Security by Design | PASS | Design has explicit authorization, acknowledgement, preview, transaction, audit, and remote fetch controls. |
| II. Test-First, Risk-Based Verification | PASS | Slices require API/integration, contract, unit, and AdminWeb E2E tests before implementation. |
| III. Layered Architecture with Vertical Feature Slices | PASS | Plan uses five backend+AdminWeb vertical slices. |
| IV. Simplicity and Dependency Discipline | PASS | Stays in current stack; no postponed AOT migration or banned packages. |
| V. Operational Reliability and Observability | PASS | Destructive operations are atomic and audited; remote import is bounded and trusted. |
| VI. User-Facing and API Consistency | PASS | AdminWeb label and API terminology are clarified; existing role permission string contract is preserved. |

