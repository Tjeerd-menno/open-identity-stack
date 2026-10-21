# Repository Guidelines

## Project Structure & Module Organization
`src/` contains the product code. Core backend layers live in `SharedKernel`, `OpenIdentityStack.Domain`, `OpenIdentityStack.Application`, and `OpenIdentityStack.Infrastructure`. Runtime entry points are `OpenIdentityStack.Api`, `OpenIdentityStack.DbMigrator`, `OpenIdentityStack.AppHost` (Aspire orchestration), and `OpenIdentityStack.ManagementWeb` (React/Vite admin UI). `tests/` mirrors those layers with focused projects such as `*.Domain.Tests`, `*.Application.Tests`, `*.Api.Tests`, `*.Contract.Tests`, and `*.ManagementWeb.E2ETests`. Product docs live in `docs/`, deployment scripts in `deploy/`, and API contracts in `contracts/`.

## Build, Test, and Development Commands
Use .NET 10 and restore from the solution root:

- `dotnet restore OpenIdentityStack.slnx` restores backend dependencies.
- `dotnet build OpenIdentityStack.slnx --no-restore` builds all .NET projects with analyzers enabled.
- `dotnet run --project src/OpenIdentityStack.AppHost` starts the full Aspire stack locally.
- `dotnet test --project tests/OpenIdentityStack.Domain.Tests/OpenIdentityStack.Domain.Tests.csproj` runs a single fast unit suite.
- `dotnet test --test-modules "tests/**/bin/Debug/net10.0/*Api.Tests.dll" --max-parallel-test-modules 1 --no-build --no-restore` matches the CI pattern for sequential API-style modules.
- `cd src/OpenIdentityStack.ManagementWeb; npm install; npm run dev` starts the management UI.
- `cd src/OpenIdentityStack.ManagementWeb; npm run build && npm run lint && npm test` validates the frontend.
- `mkdocs build --strict` verifies the docs site.

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
- Transactional delete or manifest flows do not rely on no-op transaction boundaries.
- Renames update user-facing labels, validation messages, tests, and contracts consistently.

Fixed sleeps and Radix `CheckAsync` are architecture rules below.

## Architecture rules

Use cases and query handlers stay `public sealed` and are Api's entry point. Repository ports stay `internal` to Application. `IApplicationsAdminWorkflow` stays because it already existed. Do not add `I*Admin` or `IInteractiveSignIn`.

Each row is a rule and the check that fails when the old shape comes back. The last three rows are judgment. They have no test.

| Rule | Enforced by |
| --- | --- |
| Playwright tests click and assert state. They do not call locator `CheckAsync`. | `AgentShapeTests.E2E_DoesNotCallPlaywrightLocatorCheckAsync` |
| Tests wait on a concrete UI or network condition. They do not call `WaitForTimeoutAsync`. | `AgentShapeTests.Tests_DoNotWaitForTimeoutAsync` |
| HTTP errors use one body, through `ErrorResultMapper.ToErrorResult`. | `AgentShapeTests.Api_HasASingleErrorResultMapper` |
| OpenAPI for applications lives in one tree, `contracts/openapi`. | `AgentShapeTests.ApplicationsOpenApi_LivesOnlyUnderContracts` |
| Management Web imports generated permission constants. A backtick literal such as `` `users:read` `` is still a hand-synced permission. | `AgentShapeTests.ManagementWeb_ImportsGeneratedPermissionConstants` |
| A single-permission CRUD route uses the generated endpoint policy. | `CrudRoutePolicyTests.CrudRoutes_UseEndpointAuthorizationPolicy` |
| Api and Authentication do not reference repository ports. Call the use case or query handler. | `RepositoryBoundaryTests.ApiAndAuthentication_DoNotReferenceRepositoryInterfaces` |
| Use-case and query-handler classes are `public sealed`. | `UseCaseSealingTests.ApplicationUseCasesAndQueryHandlers_ArePublicSealed` |
| No new `I*Admin` or `IInteractiveSignIn`. Use cases stay Api's entry point. | `AdminWrapperTests.Source_DoesNotAddAnAdminWrapper` |
| A Dockerfile's final stage is `USER app` or `USER nginx`. | `ContainerUserTests.Dockerfiles_EndAsAppOrNginx` |
| `string.Equals` and `string.Contains(string)` pass `StringComparison`. | Analyzer `StringComparisonAnalyzer` (`OIS0001`), tested by `StringComparisonAnalyzerTests` |
| Production seed does not register a demo client secret. `ShouldSeedDemoClients` returns false before `Seed:DemoClients`. | `DemoSecretSeedTests.ProductionSeedGuard_RefusesADemoSecret` |
| Logout-token tests assert the algorithm or the validation result. | `LogoutTokenSignatureTests.LogoutTokenTests_AssertASignature` |
| A test asserts an outcome Sonar S2699 accepts. `WaitForAsync` alone does not. | `S2699ReplayTests`; `.github/workflows/ci.yml` runs `dotnet-sonarscanner`, which enforces S2699 |
| The coverage gate rejects a module report with no measured lines. | `CoverageGateTests.EmptyModuleReport_FailsTheCoverageGate` and `.github/workflows/pr-validation.yml` run `scripts/ci/verify-coverage.sh`, and `.github/workflows/ci.yml` runs `scripts/ci/verify-coverage.ps1`; both scripts reject an empty module. |
| Authorize and refresh read effective permissions once, through `ITokenPermissionSnapshot`. | `TokenPermissionSnapshotTests.AuthorizeAndRefreshResolveEffectivePermissionsOnceAndKeepNameOverGuidPrecedence` and `ForcedSecondEffectiveRoleQuery_FailsTheQueryCount` |
| The five-minute fresh-authentication window stays as it is. | Judgment. ADR 0005. No check. |
| Screens that need several permissions stay explicit. The generated map covers single-permission CRUD. | Judgment. No check. |
| A denied security attempt commits its audit even when the mutation rolls back. | Judgment. No check. |

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

## Cursor Cloud specific instructions

- Toolchain on the image: .NET SDK 10.0.x (`DOTNET_ROOT=/usr/local/share/dotnet`), Node.js 22.23 with npm 11 (the management UI rejects older Node 22.14), Docker using the overlay2 driver, the Aspire CLI on `PATH`, and `mkdocs` from `/opt/open-identity-stack-docs`. `python -m mkdocs` is not installed; use the `mkdocs` command.
- `Directory.Packages.props` pins transitive `System.Security.Cryptography.Pkcs` and `System.Security.Cryptography.Xml` to 10.0.13. SDK 10.0.402 treats the 10.0.12 advisories as restore errors (`NU1902`, `NU1903`) because warnings are errors. The cloud image also sets `NuGetAudit=false`; with the pin, `dotnet restore OpenIdentityStack.slnx` succeeds with audit enabled.
- Start Docker before Aspire. Use `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` and `aspire start --non-interactive --nologo --launch-profile http --apphost src/OpenIdentityStack.AppHost`. The http launch profile is the one this VM can host; the https profile refuses to start. API health is `http://localhost:5000/health`. The management UI is `http://localhost:5175`.
- The AppHost user secret `Parameters:default-admin-password` matches `DefaultAdminPassword` in `tests/OpenIdentityStack.ManagementWeb.E2ETests/fixtures/ManagementWebAppHostFixture.cs`. The seeded account is `admin@localhost.dev`.
- The migrator does not grant `management-web-client` administrative API access unless `Seed__AdministrativeAccess__BootstrapManagementWeb=true`. After the API is healthy, run `OpenIdentityStack.DbMigrator` once with that variable, `DOTNET_ENVIRONMENT=Development`, and the API resource connection string `ConnectionStrings__openidentitystack`. Without the grant, sign-in returns OpenIddict `access_denied` (ID2015) and the UI stays signed out.
- Install frontend dependencies with `npm ci --ignore-scripts` in `src/OpenIdentityStack.ManagementWeb` and `src/frontend-packages/admin-api-client`. Use npm 11 so `npm ci` does not rewrite `package-lock.json`.
