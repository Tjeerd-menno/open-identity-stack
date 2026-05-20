# Repository Guidelines

## Project Structure & Module Organization
`src/` contains the product code. Core backend layers live in `SharedKernel`, `OpenIdentityStack.Domain`, `OpenIdentityStack.Application`, and `OpenIdentityStack.Infrastructure`. Runtime entry points are `OpenIdentityStack.Api`, `OpenIdentityStack.DbMigrator`, `OpenIdentityStack.AppHost` (Aspire orchestration), and `OpenIdentityStack.AdminWeb` (React/Vite admin UI). `tests/` mirrors those layers with focused projects such as `*.Domain.Tests`, `*.Application.Tests`, `*.Api.Tests`, `*.Contract.Tests`, and `*.AdminWeb.E2ETests`. Product docs live in `docs/`, deployment scripts in `deploy/`, and longer-form design work in `specs/`.

## Build, Test, and Development Commands
Use .NET 10 and restore from the solution root:

- `dotnet restore OpenIdentityStack.slnx` restores backend dependencies.
- `dotnet build OpenIdentityStack.slnx --no-restore` builds all .NET projects with analyzers enabled.
- `dotnet run --project src/OpenIdentityStack.AppHost` starts the full Aspire stack locally.
- `dotnet test --project tests/OpenIdentityStack.Domain.Tests/OpenIdentityStack.Domain.Tests.csproj` runs a single fast unit suite.
- `dotnet test --test-modules "tests/**/bin/Debug/net10.0/*Api.Tests.dll" --max-parallel-test-modules 1 --no-build --no-restore` matches the CI pattern for sequential API-style modules.
- `cd src/OpenIdentityStack.AdminWeb; npm install; npm run dev` starts the admin UI.
- `cd src/OpenIdentityStack.AdminWeb; npm run build && npm run lint && npm test` validates the frontend.
- `python -m mkdocs build --strict` verifies the docs site.

## Coding Style & Naming Conventions
`.editorconfig` is authoritative. Use spaces, 4-space indentation in `*.cs`, and 2 spaces in project/XML files. C# warnings are treated as errors; prefer file-scoped namespaces, braces, nullable-enabled code, PascalCase for types/members, `I`-prefixed interfaces, and camelCase private fields without underscores. In the frontend, keep React components in PascalCase files (`UserDetail.tsx`) and hooks in `useXxx.ts`.

## Testing Guidelines
The repo uses Microsoft.Testing.Platform, xUnit-style .NET tests, Vitest for the admin UI, and Playwright-based E2E coverage through `OpenIdentityStack.AdminWeb.E2ETests`. Name test files `*Tests.cs` and keep test projects aligned to the production layer they verify. Build before `--test-modules` runs so the test executables exist. Keep coverage-relevant code out of generated files and migrations.

## Commit & Pull Request Guidelines
Recent history favors short, imperative subjects such as `Add SonarQube scanning to CI workflows` or focused `Bump ...` dependency updates. Keep commits scoped and descriptive. Pull requests should explain the user-visible or operational impact, list validation performed, and link the relevant issue or spec when one exists. Include screenshots for `AdminWeb` UI changes, and call out doc or deployment updates when behavior changes.
