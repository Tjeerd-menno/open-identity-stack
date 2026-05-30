# OpenIdentityStack Contract Tests

## Purpose

This project is for API contract checks only. It should validate consumer/provider expectations and API schema shape without starting the full application stack.

Contract tests should cover:

- Pact consumer contracts using `PactHelper`
- OpenAPI/schema artifacts and examples
- Request and response field expectations
- Status codes and compatibility rules that consumers rely on

Contract tests should not cover:

- Business workflow correctness
- Persistence behavior
- Multi-step CRUD flows
- Authentication side effects
- Full-stack Aspire orchestration

Those scenarios belong in `OpenIdentityStack.Api.Tests` or higher-level E2E tests.

## Runtime Model

`OpenIdentityStack.Contract.Tests` does not use Aspire, AppHost, PostgreSQL, or the shared API test database. Current tests are static contract/specification checks, and Pact tests should use Pact mock servers.

If provider verification is added later, prefer a dedicated provider-verification setup that is explicit about whether it uses WebApplicationFactory, an externally running API, or full Aspire orchestration. Do not add Aspire back to this project for ordinary contract tests.

## Difference from API Tests

| Aspect | Contract Tests (`*.Contract.Tests`) | API Tests (`*.Api.Tests`) |
|--------|-------------------------------------|---------------------------|
| Purpose | Validate compatibility and schema expectations | Validate API behavior and workflows |
| Host | None or Pact mock server | In-process `WebApplicationFactory<Program>` |
| Database | None | Shared prefilled SQLite test database |
| Focus | Consumer expectations and API shape | Business behavior, persistence, auth, side effects |
| Example | `POST /connect/token` request includes required grant fields | Created user can be reset, disabled, queried, and deleted |

## Pact Helper

`Common/PactHelper.cs` contains shared Pact setup and common matchers. Use it when adding real Pact consumer tests that generate pact files:

```csharp
var pact = PactHelper.CreatePactBuilder("AdminWeb", "OpenIdentityStack.Api")
    .UponReceiving("A request to get a user")
    .WithRequest(HttpMethod.Get, "/api/admin/users/00000000-0000-0000-0000-000000000000")
    .WillRespond()
    .WithStatus(200)
    .WithJsonBody(new
    {
        id = PactHelper.Matchers.UniqueId(),
        email = PactHelper.Matchers.Email()
    });
```

## Running The Tests

```bash
dotnet test tests/OpenIdentityStack.Contract.Tests
```

## OpenAPI Versioning Rules

The CI scripts under `scripts/ci/compare-openapi-breaking-changes.ps1` and `.sh` compare OpenAPI specs found under `specs/**/contracts`.

- Removing an existing spec file is treated as a breaking change.
- New spec files are skipped until a base version exists on the comparison branch.
- Breaking-change comparison only runs when `info.version` is unchanged between base and current.
- When `info.version` changes, CI skips the breaking-change diff for that spec and reports the version change instead.

Use that behavior intentionally:

- Keep the old spec path in place when a rename would otherwise look like a removal.
- Bump `info.version` when you intentionally want to declare a new contract version.
- Do not treat version bumps as a way to hide accidental contract drift; pair them with a note in the PR summary.

## Placement Rule

When adding a test, place it here only if it can fail because the API contract changed in a way that affects a consumer. If the test needs a real database, verifies persisted state, or executes a multi-step workflow, put it in `OpenIdentityStack.Api.Tests`.
