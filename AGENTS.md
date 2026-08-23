# Repository Guidelines

## Project Structure & Module Organization
`src/` contains the product code. Core backend layers live in `SharedKernel`, `OpenIdentityStack.Domain`, `OpenIdentityStack.Application`, and `OpenIdentityStack.Infrastructure`. Runtime entry points are `OpenIdentityStack.Api`, `OpenIdentityStack.DbMigrator`, `OpenIdentityStack.AppHost` (Aspire orchestration), and `OpenIdentityStack.ManagementWeb` (React/Vite admin UI). `tests/` mirrors those layers with focused projects such as `*.Domain.Tests`, `*.Application.Tests`, `*.Api.Tests`, `*.Contract.Tests`, and `*.ManagementWeb.E2ETests`. Product docs live in `docs/`, deployment scripts in `deploy/`, and longer-form design work in `specs/`.

## Build, Test, and Development Commands
Use .NET 10 and restore from the solution root:

- `dotnet restore OpenIdentityStack.slnx` restores backend dependencies.
- `dotnet build OpenIdentityStack.slnx --no-restore` builds all .NET projects with analyzers enabled.
- `dotnet run --project src/OpenIdentityStack.AppHost` starts the full Aspire stack locally.
- `dotnet test --project tests/OpenIdentityStack.Domain.Tests/OpenIdentityStack.Domain.Tests.csproj` runs a single fast unit suite.
- `dotnet test --test-modules "tests/**/bin/Debug/net10.0/*Api.Tests.dll" --max-parallel-test-modules 1 --no-build --no-restore` matches the CI pattern for sequential API-style modules.
- `cd src/OpenIdentityStack.ManagementWeb; npm install; npm run dev` starts the management UI.
- `cd src/OpenIdentityStack.ManagementWeb; npm run build && npm run lint && npm test` validates the frontend.
- `python -m mkdocs build --strict` verifies the docs site.

## Coding Style & Naming Conventions
`.editorconfig` is authoritative. Use spaces, 4-space indentation in `*.cs`, and 2 spaces in project/XML files. C# warnings are treated as errors; prefer file-scoped namespaces, braces, nullable-enabled code, PascalCase for types/members, `I`-prefixed interfaces, and camelCase private fields without underscores. In the frontend, keep React components in PascalCase files (`UserDetail.tsx`) and hooks in `useXxx.ts`.

## Testing Guidelines
The repo uses Microsoft.Testing.Platform, xUnit-style .NET tests, Vitest for the management UI, and Playwright-based E2E coverage through `OpenIdentityStack.ManagementWeb.E2ETests`. Name test files `*Tests.cs` and keep test projects aligned to the production layer they verify. Build before `--test-modules` runs so the test executables exist. Keep coverage-relevant code out of generated files and migrations.

## Agent Orchestration Guidelines
For large or multi-phase work, prefer agent orchestration over a single long-running thread.

Use subagents or fleet-style/background sessions when:
- The task naturally splits across independent areas such as Domain/Application/API, ManagementWeb, contracts/docs, or verification.
- There are 2+ independent failing test files or subsystems.
- The user says "continue" after a long implementation session and there are still multiple unchecked tasks.
- `tasks.md` contains parallelizable `[P]` tasks or separate phases with non-overlapping file ownership.

Do not parallelize when:
- Multiple tasks edit the same files or shared model contracts.
- A design decision is unresolved.
- A failing test likely has one shared root cause.
- The work requires one coherent refactor across layers.

When using subagents, give each agent:
- A narrow scope and explicit file boundaries.
- The relevant spec/plan/tasks excerpts.
- The exact verification command for its slice.
- A requirement to report changed files, root cause, and validation output.

The coordinating agent remains responsible for integration, conflict resolution, final build/test/docs verification, and task checkbox updates.

## Commit & Pull Request Guidelines
Recent history favors short, imperative subjects such as `Add SonarQube scanning to CI workflows` or focused `Bump ...` dependency updates. Keep commits scoped and descriptive. Pull requests should explain the user-visible or operational impact, list validation performed, and link the relevant issue or spec when one exists. Include screenshots for `ManagementWeb` UI changes, and call out doc or deployment updates when behavior changes.
For cross-layer features, prefer smaller PR slices when the work naturally separates into contracts/domain/application, infrastructure/migrations, and ManagementWeb/E2E. Large PRs make review and CI failures harder to localize.
Before opening a PR, sanity-check the changes against the repo's recurring failure modes:
- OpenAPI contract and versioning changes are intentional and documented.
- E2E tests wait on concrete UI or network conditions instead of fixed sleeps.
- Radix-based controls are exercised with role/state-aware selectors instead of native input helpers.
- Transactional delete or manifest flows do not rely on no-op transaction boundaries.
- Renames update user-facing labels, validation messages, tests, and contracts consistently.

## Agent skills

### Issue tracker

Issues are tracked in GitHub Issues (Tjeerd-menno/open-identity-stack) via the `gh` CLI; external PRs are not pulled into `/triage`. See `docs/agents/issue-tracker.md`.

### Triage labels

Default label vocabulary — `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix` — created as new GitHub labels where missing. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.

## Project Principles

OpenIdentityStack is an identity and access management product. These principles govern all feature work, migrated from the former Spec Kit constitution.

### I. Security by Design
Authentication, authorization, secrets, certificates, token handling, session management, auditability, and safe error behavior are product correctness requirements. Every feature touching identity, access, administration, configuration, or runtime behavior must define its security impact, permission boundaries, audit events for security-relevant operations, and failure modes. Secrets must not be stored or logged in plaintext. User-facing and API errors must not enable account enumeration or disclose sensitive operational details. Privileged operations must be authenticated, authorized, and auditable.

### II. Test-First, Risk-Based Verification
Tests must be planned before implementation. Behavior changes must include test tasks before implementation tasks unless the change is low-risk documentation or a mechanical update. Domain and application logic must have focused unit tests. API behavior must have integration tests. Public API shape changes must have contract tests when the contract is externally visible or consumed by ManagementWeb or third-party clients. ManagementWeb workflows must use Vitest, Playwright, or both based on user impact and risk.

### III. Layered Architecture with Vertical Feature Slices
Backend changes must preserve the existing Clean/Hexagonal layering: Domain contains business rules and domain types; Application contains use cases, queries, commands, DTOs, and ports; Infrastructure contains persistence, OpenIddict, external provider, audit, and other adapter implementations; Api contains HTTP and UI adapters; AppHost orchestrates the local Aspire runtime. Features should be organized by domain slice (Users, Groups, Roles, Sessions, ServiceAccounts, Federation, Clients, ServicePermissions). Dependencies must point inward — Domain code must not depend on ASP.NET Core, EF Core, OpenIddict, React, or other adapter concerns.

### IV. Simplicity and Dependency Discipline
Prefer direct services and use cases, explicit DTO mapping, System.Text.Json, Microsoft OpenAPI plus Scalar, and existing repository patterns. Disallowed packages (require an explicit, documented exception to introduce): Newtonsoft.Json (use System.Text.Json), Swashbuckle/Swagger (use Microsoft.AspNetCore.OpenApi and Scalar), AutoMapper (use explicit mapping), MediatR (use direct use-case/service/query/command handler injection). New packages must be justified by irreplaceable value, maintenance health, security posture, license compatibility, and compatibility with .NET 10 or the ManagementWeb stack.

### V. Operational Reliability and Observability
Features affecting runtime behavior must include health, logging, diagnostics, configuration, migration, deployment, and rollback considerations proportional to risk. Production paths must preserve PostgreSQL persistence, data-protection storage, signing and encryption key safety, reverse-proxy behavior, and secure configuration. Security-relevant and operationally significant actions must be observable without exposing secrets or sensitive personal data.

### VI. User-Facing and API Consistency
Admin APIs must use consistent resource shapes, validation, pagination, Problem Details-style errors, authorization behavior, and OpenAPI documentation. ManagementWeb changes must follow the existing React/Vite feature-folder structure, use established UI components and hooks, and keep workflows accessible, predictable, and testable. User-facing behavior must be specified with independently testable scenarios and measurable outcomes.

<!-- sonar:begin:codex-secrets-on-read -->
# SonarQube secrets scanning for files protocol

Before reading any file in this workspace, scan it for secrets with the deterministic scanner:

```bash
sonar analyze secrets <path/to/file>
```

If the command reports that the file contains a secret, **do not read the file**. Instead:

1. Inform the user that the file appears to contain a secret or credential and that reading it would expose the value in chat history, logs, and any downstream telemetry.
2. Advise them to rotate the leaked credential at its source of truth and remove it from the file.
3. Do not proceed with the original request until the secret has been removed.
<!-- sonar:end:codex-secrets-on-read -->
