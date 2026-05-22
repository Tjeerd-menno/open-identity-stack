# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]

**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for OpenIdentityStack. Prefer existing repo patterns and keep any deviations
  explicit in the Constitution Check.
-->

**Language/Version**: [.NET 10 / C# latest for backend; TypeScript for AdminWeb, or NEEDS CLARIFICATION]

**Primary Dependencies**: [OpenIddict, ASP.NET Core, EF Core, Aspire, React/Vite, or NEEDS CLARIFICATION]

**Storage**: [PostgreSQL via EF Core/Aspire, browser runtime config, or N/A]

**Testing**: [Microsoft.Testing.Platform/xUnit-style tests, API/integration tests, contract tests, Vitest, Playwright, or NEEDS CLARIFICATION]

**Target Platform**: [Linux/containerized backend, Windows service package, browser AdminWeb, or NEEDS CLARIFICATION]

**Project Type**: [Identity/API backend, AdminWeb frontend, Aspire orchestration, docs/deployment, or NEEDS CLARIFICATION]

**Performance Goals**: [auth/API latency, throughput, page interaction, startup behavior, or NEEDS CLARIFICATION]

**Constraints**: [security, package bans, warnings-as-errors, analyzer compliance, deployment/runtime constraints, or NEEDS CLARIFICATION]

**Scale/Scope**: [users, clients, sessions, groups, API endpoints, AdminWeb screens, or NEEDS CLARIFICATION]

**Security Impact**: [auth/authz, permissions, secrets, certificates, tokens, sessions, audit events, safe errors, or N/A]

**Operational Impact**: [health, logging, diagnostics, configuration, migrations, deployment, rollback, Aspire behavior, or N/A]

**Documentation Impact**: [docs/, deploy/, README/AGENTS, specs only, or N/A]

**Package/Dependency Changes**: [new/updated packages with justification, or N/A]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Status | Evidence / Notes |
|------|--------|------------------|
| I. Security by Design | [PASS/FAIL] | [Security impact, permission boundaries, audit events, failure modes, safe errors] |
| II. Test-First, Risk-Based Verification | [PASS/FAIL] | [Tests planned before implementation; low-risk exception rationale if no tests] |
| III. Layered Architecture with Vertical Feature Slices | [PASS/FAIL] | [Layer ownership, dependency direction, domain slice organization] |
| IV. Simplicity and Dependency Discipline | [PASS/FAIL] | [Existing patterns used; banned packages avoided; new dependency rationale] |
| V. Operational Reliability and Observability | [PASS/FAIL] | [Health, logging, diagnostics, config, migrations, deployment, rollback] |
| VI. User-Facing and API Consistency | [PASS/FAIL] | [API shape, validation, pagination, Problem Details, OpenAPI, AdminWeb consistency] |
| Technology and Package Constraints | [PASS/FAIL] | [.NET 10/AdminWeb stack compatibility, central package management, analyzers] |
| Documentation Impact | [PASS/FAIL] | [docs/, deploy/, README/AGENTS, specs updates needed] |
| Validation Commands | [PASS/FAIL] | [Exact build/test/lint/docs commands required for this feature] |

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused areas and expand chosen paths with real
  project names. The delivered plan must not include unused options.
-->

```text
# Backend layers (use as applicable)
src/
├── SharedKernel/
├── OpenIdentityStack.Domain/
├── OpenIdentityStack.Application/
├── OpenIdentityStack.Infrastructure/
├── OpenIdentityStack.Api/
├── OpenIdentityStack.DbMigrator/
├── OpenIdentityStack.ServiceDefaults/
└── OpenIdentityStack.AppHost/

tests/
├── OpenIdentityStack.Domain.Tests/
├── OpenIdentityStack.Application.Tests/
├── OpenIdentityStack.Infrastructure.Tests/
├── OpenIdentityStack.Api.Tests/
├── OpenIdentityStack.Contract.Tests/
└── OpenIdentityStack.AdminWeb.E2ETests/

# AdminWeb (use when frontend changes are included)
src/OpenIdentityStack.AdminWeb/
├── src/features/[feature]/
├── src/components/
├── src/lib/
└── src/routes/

# Docs/deployment (use when behavior changes require updates)
docs/
deploy/
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [constitution gate or package constraint] | [current need] | [simpler conforming alternative and why insufficient] |
