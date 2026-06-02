# Implementation Plan: Management Web AdminWeb Parity

**Branch**: `[008-management-web]` | **Date**: 2026-06-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-management-web/spec.md`

## Summary

Build ManagementWeb into the Mantine-based successor to AdminWeb by porting AdminWeb behavior one vertical slice at a time. Shared ManagementWeb infrastructure comes first: normalized admin API client behavior, authorization helpers, route guards, table/form/dialog primitives, theme handling, error handling, one-time secret display, and cross-UI authentication continuity. After that, slices are delivered with a strict definition of done: AdminWeb-equivalent behavior, Mantine presentation, targeted unit/component/API coverage, and good E2E coverage.

ManagementWeb uses only the consolidated Applications model through `/api/admin/applications`; it must not expose Clients or Service Accounts. ManagementWeb also adds a read-only Audit section backed by a new `GET /api/admin/audit-entries` endpoint.

## Technical Context

**Language/Version**: TypeScript/React for ManagementWeb and AdminWeb parity references; .NET 10/C# for the new audit query API.

**Primary Dependencies**: Mantine UI, React, Vite, TanStack Query, oidc-client-ts, existing Admin API, Aspire-managed backend services, Vitest, Playwright, Microsoft.Testing.Platform/xUnit-style .NET tests.

**Storage**: Browser storage for theme preference and OIDC session state. PostgreSQL remains the system of record for admin data. Audit reads use existing `AuditLogEntries`.

**Testing**: Vitest for frontend unit/component behavior, Playwright for ManagementWeb E2E workflows, .NET API/integration and contract tests for the new audit endpoint, and existing AdminWeb tests as the parity baseline.

**Target Platform**: Browser-based operator UI alongside the existing AdminWeb during transition, with Linux/containerized backend services and Aspire local orchestration.

**Project Type**: Management frontend expansion plus one backend read API.

**Performance Goals**: Initial page load under 2 seconds for completed slices in production-like tests; table interactions under 300 ms for typical admin data volumes; audit filtering remains paginated and bounded.

**Constraints**: Preserve layered backend ownership, keep both UIs independently deployable, use Mantine for the new frontend visual layer, avoid legacy Clients/Service Accounts in ManagementWeb, keep warnings-as-errors and nullable compliance intact.

**Scale/Scope**: Full ManagementWeb parity for AdminWeb domains that still exist: Users, Roles, Groups, Applications, Application Permissions, Sessions, Providers, Settings, and Overview/Dashboard. Adds ManagementWeb-only Audit backed by `GET /api/admin/audit-entries`.

**Security Impact**: High. ManagementWeb gates privileged operator actions, displays one-time application secrets, and exposes audit trail data. Backend authorization remains authoritative and audit reads require `audit-logs:read`.

**Operational Impact**: Requires coordinated rollout of frontend parity slices and the audit query API. AdminWeb remains available until full parity and burn-in are achieved.

**Documentation Impact**: Requires updated ManagementWeb operator docs, route/domain language, screenshots, rollout guidance, and AdminWeb decommission criteria.

**Package/Dependency Changes**: No new backend package families are planned. ManagementWeb continues to use the existing Mantine/React/Vite stack.

## Constitution Check

*GATE: Must pass before implementation. Re-check after design updates.*

| Gate | Status | Evidence / Notes |
|------|--------|------------------|
| I. Security by Design | PASS | Backend authorization remains authoritative, secrets are one-time display only, permissions are normalized centrally, and Audit requires `audit-logs:read`. |
| II. Test-First, Risk-Based Verification | PASS | Every slice requires tests before completion, with strong E2E coverage for operator-critical flows and API/contract tests for audit. |
| III. Layered Architecture with Vertical Feature Slices | PASS | ManagementWeb remains a delivery adapter over existing admin APIs; Audit adds a read-only API/application/infrastructure query path. |
| IV. Simplicity and Dependency Discipline | PASS | Reuses current React/Vite/Mantine stack and existing backend patterns. No legacy compatibility UI is introduced. |
| V. Operational Reliability and Observability | PASS | AdminWeb remains independently deployable; ManagementWeb slices are independently verifiable; Audit uses paginated reads. |
| VI. User-Facing and API Consistency | PASS | Existing AdminWeb behavior is the parity baseline, Applications use consolidated terminology, and routes remain stable where domains exist. |
| Technology and Package Constraints | PASS | Fits .NET 10 plus current frontend package constraints. |
| Documentation Impact | PASS | Operator docs, screenshots, and rollout/decommission guidance are in scope. |
| Validation Commands | PASS | Quickstart contains backend, frontend, E2E, and docs validation commands. |

## Project Structure

### Documentation (this feature)

```text
specs/007-management-web/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── management-web.md
└── tasks.md
```

### Source Code (repository root)

```text
src/
├── OpenIdentityStack.ManagementWeb/
│   └── src/
│       ├── components/
│       ├── features/
│       ├── lib/
│       └── routes/
├── OpenIdentityStack.AdminWeb/
├── OpenIdentityStack.Api/
├── OpenIdentityStack.Application/
├── OpenIdentityStack.Infrastructure/
├── OpenIdentityStack.Domain/
├── OpenIdentityStack.AppHost/
└── OpenIdentityStack.DbMigrator/

tests/
├── OpenIdentityStack.ManagementWeb.E2ETests/
├── OpenIdentityStack.AdminWeb.E2ETests/
├── OpenIdentityStack.Api.Tests/
├── OpenIdentityStack.Contract.Tests/
├── OpenIdentityStack.Application.Tests/
├── OpenIdentityStack.Infrastructure.Tests/
└── OpenIdentityStack.Domain.Tests/

docs/
deploy/
```

**Structure Decision**: Expand `src/OpenIdentityStack.ManagementWeb` as a peer frontend to `src/OpenIdentityStack.AdminWeb`, port behavior slice-by-slice, and add the audit read endpoint in the backend using existing layered patterns.

## Complexity Tracking

No constitution violations are required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | N/A | N/A |

## Phase 0 Research Summary

Key decisions from repo inspection and grilling:

- ManagementWeb should reach AdminWeb functional parity before AdminWeb decommission.
- Behavior parity is more important than visual parity; ManagementWeb uses Mantine while preserving AdminWeb workflows.
- Shared foundation must be upgraded before new vertical slices.
- ManagementWeb uses only `/api/admin/applications` for application-like resources.
- No Clients or Service Accounts navigation exists in ManagementWeb.
- Applications remains one list with filters and profile-aware forms, matching AdminWeb behavior.
- Permission checks are normalized in ManagementWeb foundation; backend authorization remains authoritative. ManagementWeb reads granular grants from `permission`, `permissions`, `scope`, and `scp` claims in both OIDC profile data and access-token payloads. It does not infer authorization from role names such as `admin` or `super-admin`; the backend must emit concrete effective permissions into the token.
- Existing Users code is partial and must be refactored into the new foundation.
- Audit is a ManagementWeb-only addition backed by a new read-only `GET /api/admin/audit-entries`.
- `GET /api/admin/audit-entries` uses normal page/pageSize pagination, supports filters, requires `audit-logs:read`, and includes `details`, `beforeState`, and `afterState` in v1 list items.

## Phase 1 Design Summary

Design artifacts are updated:

- [data-model.md](./data-model.md): ManagementWeb foundation, navigation, vertical slice, application, audit, and theme state models.
- [contracts/management-web.md](./contracts/management-web.md): route, behavior, parity, and audit endpoint contract.
- [quickstart.md](./quickstart.md): validation commands for backend, frontend, E2E, and docs.

### Implementation Sequencing

1. **Shared foundation parity**: Normalize ManagementWeb API client, errors, auth, token-claim permission extraction, route guards, permission helpers, reusable Mantine primitives, theme, and secret display.
2. **Applications**: Port consolidated Applications behavior from AdminWeb using only `/api/admin/applications`.
3. **Users parity refactor**: Refactor the partial Users slice into the new foundation and complete AdminWeb parity.
4. **Roles**: Port role CRUD and permission selector/catalog behavior.
5. **Groups**: Port group CRUD, members, and mappings.
6. **Sessions**: Port session list/detail/revoke workflows.
7. **Providers**: Port identity provider CRUD/status workflows.
8. **Settings**: Port authentication settings workflows.
9. **Application Permissions**: Port registry, manifests, ownership, maintainers, diagnostics, and history workflows.
10. **Audit**: Add `/api/admin/audit-entries` and ManagementWeb audit list/filter/expand UI.
11. **Overview/Dashboard**: Add aggregate overview and quick links after underlying slices are reliable.
12. **Docs and rollout**: Update operator documentation, screenshots, release notes, and decommission criteria.

### Slice Definition Of Done

A vertical slice is complete only when:

- ManagementWeb behavior matches AdminWeb for the same domain, except for explicitly decided changes such as consolidated Applications.
- Routes and navigation are wired with AdminWeb-compatible paths where the domain still exists.
- Permission-gated actions use the shared normalized permission matrix.
- Loading, empty, error, and access-denied states are covered.
- Unit/component/API-client or backend contract tests cover risky behavior.
- Good E2E coverage exists for operator-critical workflows.
- Validation commands relevant to the slice pass.

### Phase 5 Roles Completion Note

As of 2026-06-02, the Roles slice is implemented in ManagementWeb with Mantine components under `src/OpenIdentityStack.ManagementWeb/src/features/roles/`. It preserves `/roles`, `/roles/new`, and `/roles/:id`, uses `/api/admin/roles` plus `/api/admin/permissions/platform`, keeps backend authorization authoritative, requires wildcard acknowledgement for broad grants, hides delete for system roles, and is covered by Vitest API/component/route tests plus .NET/xUnit Playwright E2E coverage.

### Phase 6 Groups Completion Note

As of 2026-06-02, the Groups slice is implemented in ManagementWeb with Mantine components under `src/OpenIdentityStack.ManagementWeb/src/features/groups/`. It preserves `/groups`, `/groups/new`, `/groups/:id`, and `/groups/:id/edit`, uses `/api/admin/groups` for group CRUD, `/api/admin/groups/{id}/members` with `/api/admin/users` for member management, and `/api/admin/groups/{id}/mappings` with `/api/admin/roles` for role mapping selection. It gates list/detail/write/delete/member-management actions with granular permissions and is covered by Vitest API/component/route tests plus .NET/xUnit Playwright E2E coverage.

### Phase 7 Sessions Completion Note

As of 2026-06-02, the Sessions slice is implemented in ManagementWeb with Mantine components under `src/OpenIdentityStack.ManagementWeb/src/features/sessions/`. It preserves `/sessions` and `/sessions/:id`, ports the AdminWeb operator workflow for list/search/status filter/detail/single-session revoke/user-wide revoke, gates destructive actions with `sessions:revoke`, and uses the current backend contract: `GET /api/admin/sessions`, `GET /api/admin/sessions/{id}`, `DELETE /api/admin/sessions/{id}`, and `DELETE /api/admin/users/{userId}/sessions`. It is covered by Vitest API/component/route tests plus .NET/xUnit Playwright E2E coverage.

### Design Re-check Constitution Gate

| Gate | Status | Evidence / Notes |
|------|--------|------------------|
| I. Security by Design | PASS | Permission normalization, backend policy authority, audit read authorization, and one-time secret handling are explicit. |
| II. Test-First, Risk-Based Verification | PASS | Slice DoD requires tests and E2E before completion. |
| III. Layered Architecture with Vertical Feature Slices | PASS | Each slice has narrow frontend ownership and backend changes only where needed, such as Audit. |
| IV. Simplicity and Dependency Discipline | PASS | No duplicate Clients/Service Accounts UI, no second audit detail endpoint in v1. |
| V. Operational Reliability and Observability | PASS | Independent deployability and AdminWeb coexistence stay in scope. |
| VI. User-Facing and API Consistency | PASS | AdminWeb paths and behavior remain the parity baseline; Applications follows consolidated API terminology. |
| Technology and Package Constraints | PASS | Uses existing technology choices. |
| Documentation Impact | PASS | Docs and screenshots are planned. |
| Validation Commands | PASS | Quickstart contains concrete commands. |
