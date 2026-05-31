# Implementation Plan: Management Web Foundation

**Branch**: `[008-management-web]` | **Date**: 2026-05-30 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-management-web/spec.md`

## Summary

Build a new Mantine-first Management Web app alongside AdminWeb, with its own hostname, OIDC client, and theme preference handling. The first release centers on the Users vertical slice, cross-UI sign-in continuity, and a production-grade operator experience while keeping the existing AdminWeb available during transition.

## Technical Context

**Language/Version**: TypeScript/React for the new frontend; .NET 10/C# for backend integration points.

**Primary Dependencies**: Mantine UI, React, Vite, existing Admin API, Aspire-managed backend services, Vitest, and Playwright.

**Storage**: Browser storage for theme preference; backend PostgreSQL remains the system of record for management data.

**Testing**: Vitest for unit/component behavior, Playwright for operator flows, and existing .NET test suites for backend contract and integration coverage.

**Target Platform**: Browser-based operator UI alongside the existing desktop/browser admin experience, with Linux/containerized backend services.

**Project Type**: New management frontend app with supporting docs and deployment configuration updates.

**Performance Goals**: Fast initial Users-page load, responsive list interactions, and seamless cross-UI authentication handoff without repeated sign-in.

**Constraints**: Preserve layered backend ownership, keep both UIs independently deployable, avoid banned package introductions on the backend, and keep warnings-as-errors / nullable compliance intact.

**Scale/Scope**: Phase 1 covers the Users vertical slice, theme controls, separate hostnames, and parallel AdminWeb/Management Web rollout; later domains remain placeholders.

**Security Impact**: Operator access must respect the same authorization model as AdminWeb, with backend policy remaining authoritative and no sensitive data exposed through theme or error handling.

**Operational Impact**: Requires independent hosting, client registration, health/telemetry separation, and rollout/rollback guidance for the new UI.

**Documentation Impact**: Requires updates to domain language, rollout guidance, and UI screenshots / operator docs for the new frontend.

**Package/Dependency Changes**: Add Mantine-related frontend packages for the new app; no new backend package families are planned.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Status | Evidence / Notes |
|------|--------|------------------|
| I. Security by Design | PASS | Management Web uses the existing authorization model, separate client registration, safe failure messages, and persistent theme choice only. |
| II. Test-First, Risk-Based Verification | PASS | Plan calls for frontend unit and E2E coverage first, with backend contract coverage where shared API behavior is exercised. |
| III. Layered Architecture with Vertical Feature Slices | PASS | The new UI is a delivery adapter over existing backend slices; no inward dependency violations are introduced. |
| IV. Simplicity and Dependency Discipline | PASS | The plan stays close to the current React/Vite stack and only adds Mantine-family frontend packages. |
| V. Operational Reliability and Observability | PASS | Separate host, independent deployment, and clear rollback/documentation expectations are included. |
| VI. User-Facing and API Consistency | PASS | The Users slice, theme controls, and cross-UI sign-in flow are specified as independently testable operator journeys. |
| Technology and Package Constraints | PASS | The plan fits .NET 10 plus the current frontend stack and keeps backend package constraints intact. |
| Documentation Impact | PASS | The feature requires docs and screenshot updates, which are included in scope. |
| Validation Commands | PASS | Validation commands are defined in quickstart.md for backend, frontend, E2E, and docs checks. |

## Project Structure

### Documentation (this feature)

```text
specs/007-management-web/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── management-web.md
└── tasks.md             # Created later by /speckit-tasks, not by this command
```

### Source Code (repository root)

```text
src/
├── OpenIdentityStack.AdminWeb/
├── OpenIdentityStack.ManagementWeb/
├── OpenIdentityStack.Api/
├── OpenIdentityStack.Application/
├── OpenIdentityStack.Infrastructure/
├── OpenIdentityStack.Domain/
├── OpenIdentityStack.AppHost/
└── OpenIdentityStack.DbMigrator/

tests/
├── OpenIdentityStack.AdminWeb.E2ETests/
├── OpenIdentityStack.Api.Tests/
├── OpenIdentityStack.Contract.Tests/
├── OpenIdentityStack.Application.Tests/
├── OpenIdentityStack.Infrastructure.Tests/
└── OpenIdentityStack.Domain.Tests/

docs/
deploy/
```

**Structure Decision**: Add `src/OpenIdentityStack.ManagementWeb` as a peer frontend to `src/OpenIdentityStack.AdminWeb`, keep the existing backend layers unchanged, and place feature documentation under `specs/007-management-web/` with a small UI contract doc under `contracts/`.

## Complexity Tracking

No constitution violations are required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | N/A | N/A |

## Phase 0 Research Summary

All technical context unknowns are resolved in [research.md](./research.md). Key decisions:

- Build Management Web as a separate peer frontend, not a replacement for AdminWeb.
- Make Mantine the primary UI foundation and keep the app production-oriented.
- Persist the operator appearance preference with light/dark/system modes.
- Keep AdminWeb and Management Web on separate hostnames with cross-UI sign-in continuity.
- Use the existing admin API as the backend source of truth for permissions and user workflows.

## Phase 1 Design Summary

Design artifacts are generated:

- [data-model.md](./data-model.md): operator-facing UI entities, theme preference state, and navigation surfaces.
- [contracts/management-web.md](./contracts/management-web.md): route and behavior contract for the new management frontend.
- [quickstart.md](./quickstart.md): validation and rollout commands.

### Implementation Sequencing

1. **Test-first frontend shell**: Add failing tests for theme preference, route structure, and cross-UI auth expectations.
2. **Frontend app scaffold**: Create `OpenIdentityStack.ManagementWeb` with Mantine layout, shell navigation, and theme handling.
3. **Users slice**: Implement the first management workflow set with list/detail/edit behavior and existing role assignment.
4. **Shared backend integration**: Wire the new frontend to the existing admin API and keep authorization behavior aligned.
5. **Cross-UI rollout**: Configure separate hostname/client settings and verify no re-login is required between UIs.
6. **Documentation and screenshots**: Update operator docs and release materials for the new frontend and rollout posture.

### Design Re-check Constitution Gate

| Gate | Status | Evidence / Notes |
|------|--------|------------------|
| I. Security by Design | PASS | Security boundary is preserved through backend authorization, separate client registration, and safe error behavior. |
| II. Test-First, Risk-Based Verification | PASS | Design maps to frontend unit/E2E tests and shared backend integration coverage. |
| III. Layered Architecture with Vertical Feature Slices | PASS | The new frontend is a delivery layer over existing slices, with no domain-layer leakage. |
| IV. Simplicity and Dependency Discipline | PASS | Mantine is the only major new UI dependency family; existing patterns remain intact. |
| V. Operational Reliability and Observability | PASS | Separate hosting, telemetry, and rollout guidance are explicit. |
| VI. User-Facing and API Consistency | PASS | The Users journey, theme controls, and cross-UI experience are specified in user terms. |
| Technology and Package Constraints | PASS | The design stays within the repo's frontend/backend technology constraints. |
| Documentation Impact | PASS | Docs and screenshots are planned as part of the feature. |
| Validation Commands | PASS | Quickstart includes the commands needed to validate the implementation. |
