# Agent-friendly architecture plan

Status: proposal for review. This document does not change runtime code. Execution waits for the owner.

Assumption: every contributor is an agent that sees only the files it opened, copies the nearest example, and takes the shortest path that compiles. A change that looks right in one file must be right for the whole repo.

Evidence labels: **measured** means a command or file read in this session. **Inferred** means a conclusion from those reads. **Guess** means neither. Commit SHAs are on `origin/main` unless noted. Review text is from GitHub pull-request review comments read in this session. A class is listed only when it happened at least twice.

## What recall could and could not see

`cursor-cloud` `list-cloud-agents` (archived included, limit 200) returned 5 agents, `totalMatched` 5, `hasMore` false. The tool is scoped to this run's environment. Accessible runs: environment setup `bc-fdd4b829-ce70-483a-b193-51d3a027323a`, build verify `bc-b8dab39d-3e2d-5183-9f09-0a4efe77413a`, NuGet pin `bc-2bfab033-a482-5df4-afea-88263248df49`, plus this plan's coordinator and worker. Transcripts for the first three were fetched and summarized. They cover toolchain traps (NuGet audit, npm 11, Aspire HTTP profile, management-web bootstrap grant). They do not contain an earlier architecture redesign.

Not accessible: desktop or IDE chats, agents outside this environment, and any older owner conversation about this repo. Review comments were read for the historical sample that the pulls-comments API returned (highest PR in that truncated sample was 335) and, separately, for PRs 398, 423, 427, 434, 465, 466, 469, 477, 479, 480, 491, 498, 522, and 525. PRs 427, 434, 477, and 479 had no review comments in that call.

## Why the current shape exists

The product is Clean Architecture on purpose. Domain and Application must not reference ASP.NET, EF Core, or OpenIddict. `LayeringTests` enforces that (`526cae9`, PR #398). Management Web is the only operator UI (ADR 0001, ADR 0002). Admin API breaks ship with the client and the UI together (ADR 0003). The browser treats access tokens as opaque and reads permissions from `GET /api/me` (ADR 0004). Linking, disablement, administrative entitlements, audiences, quarantine, and email trust are explicit (ADR 0005). Clients and service accounts were unified into Applications (`400c9d9`, PR #232). Those boundaries stay.

What an agent sees today is two templates for one job. Users, roles, and groups expose one `I*UseCase` per command. Applications expose `IApplicationsAdminWorkflow` (45 lines, ten methods). Application permissions expose a workflow and also inject maintenance use cases. **Measured:** 115 lines match `public interface I` under Application, 39 files declare an `I*UseCase`, and 2 files declare an `I*Workflow`. `UsersApi.cs` is 559 lines. The nearest file is a legal second way.

## Pitfall classes

Ranked by severity first, then by how often the class showed up.

### 1. A second path skips the invariant

Count: 4. Severity: high. Security bugs compile and look like the nearby example.

- PR #87, `b2e9dbc`. Review: `RegisterServiceUseCase` had an allow-all constructor, and a non-GUID actor id skipped the permission check.
- PR #101, `4ddefa2`. Review: MVC and OpenIddict both claimed `/connect/introspect`, so the rate limit and the permission handler could miss the request that actually ran.
- PR #465, `14c04c7`. Review: authority revision was evaluated after the permission read, so a revoke could lose the race. The same PR's review also found `ConfigureOAuth` mutating a tracked client without `SaveChangesAsync`.
- PR #491, `87ed99b`. Review, P1: collapsing "no human-subject claim" and "two human-subject claims" into null sent an administrative token down the application-permission branch. The merge message says the same PR closed that hole. The helper still documents that other callers may collapse duplicates.

Fix level: **(a) architecture.** One public workflow type per aggregate. Repository ports become `internal` to Application, with `InternalsVisibleTo` only for Infrastructure and the test projects. Api then cannot inject `IUserRepository`. A second allow-all constructor cannot be public. Types alone do not work: a second public method still compiles. Docs did not work: the introspect split and the claim helper both landed while the layering docs were already in the repo.

### 2. Secrets and sensitive snapshots get logged or shipped

Count: 4. Severity: high.

- PR #25, `aa9de0e`. Review: the migrator logged the generated admin password.
- PR #398, `526cae9`. Back-channel logout tokens were unsigned `alg: none` JWTs on the live path. A client-side `MockAuthProvider` could grant `*`. Demo client secrets were seedable in Production. The commit removes those.
- PR #424, `3ee58ff`. Hardcoded credentials and file ownership.
- PR #469, `d80a2c8`. Review: do not log the full emergency-access snapshot. PR #522, `e7e036a`, is a later scan pass and is the same pressure, not a fifth root cause.

Fix level: **(a) for the bypass, (b) for logs.** One interactive sign-in path. No build-time auth switch. A source check fails if Production seed can register a demo secret, and if logout token tests do not assert a signature. **Inferred:** a type cannot forbid `LogInformation(password)` once `string` is in scope. The check has to name `LogoutTokenFactory` and the Production seed guard. Do not add a prose rule for this. #398 already rewrote the prose by deleting the bypass.

### 3. Tests pass without checking the behavior

Count: 4. Severity: high, because classes 1 and 2 hide inside them.

- PR #101, `4ddefa2`. Review: the route test mapped only MVC controllers, so it passed when OpenIddict owned the real endpoint.
- PR #143, `85e46a1`. Review: the E2E project test only checked that Playwright files existed.
- PR #398, `526cae9`. `GroupClaimsTests` had comment-only bodies. Logout notifier tests checked counts and URLs and not the token signature. The commit deletes the empty tests and adds signature checks.
- PR #427, `9120ea8`. Sonar S2699 reported 54 tests with no assertion the analyzer accepts.

Fix level: **(c) tests, with the existing S2699 gate.** A vacuous test compiles. Architecture cannot make "assert something true" a type. Keep S2699 in CI. When a security path is touched, the test must assert the outcome (signature, status, persisted row), not that a file exists. **Not measured here:** Sonar was not re-run. The proof that S2699 catches this class is the #427 commit itself. Phase 1 should replay one of those empty tests and show the gate fail before anyone trusts it again.

### 4. Hot paths issue another query or allocation per row or per call

Count: 7. Severity: high on the token path, medium on admin lists.

- PR #87, `b2e9dbc`. Review: `GetDependenciesAsync` loaded every role and filtered in memory.
- PR #144, `0fbe0f6`. Review: each list row called both `useUser` and `useGroup`.
- PR #434, `a950bd3`. Ordinal comparisons, `FrozenDictionary`, cached `JsonSerializerOptions`.
- PR #477, `88eac46`. Token-path role queries batched; read-only queries use `AsNoTracking`.
- PR #479, `ed5f16a`. One scoped group load per request; more N+1 batching; Management Web initial JS described in the message as 890 kB to 399 kB (**not re-measured**).
- PR #491, `87ed99b`. Regex, claim, and permission-match allocations. This is the same commit as the claim-collapse bug in class 1.
- PR #498, `c01058c`. Refresh-grant role queries, and list search debounced off per-keystroke requests.

Fix level: **(a) one snapshot owner, (c) one query-count test.** Hiding repositories stops a second feature from querying users itself. It does not stop the snapshot owner from calling the database twice. Add one test on authorize and refresh that fails if effective roles are resolved more than once. A lint cannot see a loop. Docs did not stop #477, #479, #491, and #498 from being separate cleanup passes.

### 5. Retired names and deleted stacks stay copyable

Count: 4. Severity: high for agents, because the old file is a template.

- PR #113, `e78a8a2`. Review: validation text still said "Service identifier" after the rename to applications. The review thread names commit `2ec9050`, which is **not** on `origin/main` (squashed). The merge commit is the evidence.
- PR #200, `b5f1922`. Review: release workflows still advertised Admin Web after the UI was removed.
- PR #223, `238ccbd`. Review: a Dockerfile under `ManagementWeb.Legacy` built Management Web.
- PR #232, `400c9d9`. The Client and ServiceAccount stack was still in the tree and had to be deleted. `FrontendTopologyTests` (started in `60f0200`, PR #239, strengthened in `526cae9`, PR #398) now fails the build if `AdminWeb` or `ManagementWeb.Legacy` reappears. **Measured:** those tests passed inside the throwaway architecture run below.

Fix level: **(a) delete the old way, (b) keep the topology test.** The test already names the banned modules. Extend it only when a module is actually gone. Leave `ApplicationType` legal in `OpenIddictApplicationProjection` only. Product code uses `ApplicationProfile`. A doc paragraph is how the next agent learns the old word. The copilot instructions may keep one sentence that points at the projection file, because the protocol name is an external constraint, not a second product model.

### 6. Coverage or CI says pass while the data was dropped

Count: 3. Severity: high. This is the same failure mode as class 3, at the gate.

- PR #7, `e1a5178`. MTP coverage handling for `dotnet test --test-modules`.
- PR #398, `526cae9`. `.coverage-threshold` was never read from the directory the script searched. Defaults said 0.80 while CI passed 0.10 and 0.72. Frontend coverage was collected and not checked. `admin-api-client` tests were not on a workflow.
- PR #423, `2a1bf38`. The coverage gate discarded per-module results. PR #423 review also asked for a Sonar coverage format the server accepts.

Fix level: **(b) one gate.** Delete extra threshold files rather than documenting them. Phase 1 proves the gate: a fixture with an empty module report must fail. **Not run in this session.**

### 7. Case and culture of strings

Count: 4. Severity: medium, except the username uniqueness bug, which was high.

- PR #26, `fe940de`. Review: search used `ToUpperInvariant` on one side and `ToUpper` on the other.
- PR #67, `f6437ef`. Review: the unique index was case-sensitive while lookup was case-insensitive, so `Alice` and `alice` could both exist.
- PR #434, `a950bd3`. `Contains` on error codes moved to ordinal comparison.
- PR #479, `ed5f16a`. A redundant `LOWER()` on an already-normalized role name.

Fix level: **(b) types.** `NormalizedPreferredUsername` is the pattern that worked for #67: the database compares the normalized column. Culture bugs inside one entity are not an architecture split. `string.Equals` without `StringComparison` still compiles under `AnalysisLevel` `latest-recommended` (**measured** in `Directory.Build.props`; no `CA1307` / `CA1310` entry in `.editorconfig`). A local analyzer that rejects the two-argument `Equals` and `Contains` overloads, and tells the author to pass `StringComparison.Ordinal` or to use the normalized column, is the check. **Not run in this session.** Do not ban ordinal-ignore-case for emails. That is a product rule and stays in the domain type.

### 8. E2E uses a fixed sleep or a native checkbox API

Count: 3. Severity: medium. The written rule did not hold.

- PR #129, `dc638cd`. Review: three `CheckAsync` calls on Radix checkboxes, plus `WaitForTimeoutAsync(600)`.
- Parent of PR #200 (`b5f1922^`). **Measured** `git grep`: 7 `WaitForTimeoutAsync` calls under `tests/OpenIdentityStack.AdminWeb.E2ETests`, including comments "Give React time to process" and "Wait for save". Deleting Admin Web removed them. No check did.
- PR #465, `14c04c7`. **Measured** `git blame`: `AdministrativeAccessManagementTests.cs` line 68 is `GetByRole(AriaRole.Checkbox).CheckAsync()`, still on `3e1f0c0`. PR #141, `f91c837`, had already put the Radix and no-sleep rules in the PR template.

Fix level: **(b) a source check.** Playwright's `CheckAsync` cannot be deleted from the package. The supported helper is click the checkbox role and assert `data-state` or `aria-checked`. The check's failure names that helper. The throwaway test below failed on line 68 and passed for `WaitForTimeoutAsync` on the current tree. The same needle hits the 7 historical lines. Docs are the wrong level. They were already present when #465 copied `CheckAsync`.

### 9. A second copy of a list or contract drifts

Count: 3 review incidents, plus two copies that are still in tree. Severity: medium.

- PR #67, `f6437ef`. Review: certification image tags did not match the kustomization beside them.
- PR #332, `ed87352`. Review: S256-only discovery was not in the contract tests.
- PR #333, `228258a`. Review: new profile fields had no contract coverage. PR #465 review asked to move a new contract into `contracts/openapi` because the comparison script only watches that tree.

**Measured** structural copies that are easy to edit once:

- `contracts/openapi/006-unify-applications-model/applications.openapi.yaml` (806 lines) and `tests/OpenIdentityStack.Contract.Tests/Admin/Applications/applications.openapi.yaml` (805 lines) differ by a trailing newline only. `GetOpenApiContractPaths()` yields both.
- `PermissionSemantics.cs` (65 lines) and `permission-semantics.ts` (47 lines) are two matchers. `tests/PermissionSemantics/permission-semantics-cases.json` has 9 cases. Application `PermissionsMatchingTests` passed 22 tests in 643 ms (wall 2.83 s, warm). The Vitest file passed 11 tests in 180 ms (wall 0.77 s). **Inferred:** the 9 shared cases are inside both runs. The suites agree today. A third copy would not be caught by the type system.

Fix level: **(a) one file.** Contract tests read only `contracts/openapi`. Delete the second YAML. Permission strings are owned by `Permissions.cs`. A generated TypeScript module is what routes import. The JSON file stays as the behavior oracle. Generation is the source of truth across languages. One shared runtime assembly is not possible. Hand-editing both matchers is how class 7 comes back.

Placeholder strings `group:engineering` and `orders:read` in Management Web are input placeholders, not catalog drift (**measured**).

### 10. The same behavior is registered twice

Count: 3. Severity: medium.

- PR #85, `692e3bf`. Review: a second AppHost at the repo root, with a second `aspire.config.json`.
- PR #101, `4ddefa2`. The introspect split in class 1 is also a second registration.
- PR #140, `bd2264c`. Review: `AddCommonServices` called `AddApplication()` even though Program and the migrator already did. The author removed it.

Fix level: **(a).** One AppHost project. One `AddApplication` call per process. A test that fails when a second `AppHost.csproj` appears is enough. This is the same mechanism as class 5.

### 11. The UI gates a different permission than the API

Count: 3. Severity: high for the operator who gets a 403 or a hidden action.

- PR #143, `85e46a1`. Review: the users page always loaded roles, which require `roles:read`, and role assignment was hidden behind `users:write`.
- PR #223, `238ccbd`. Review: granular actions (group members, credentials) were not gated on their own permissions.
- PR #469, `d80a2c8`. Review: a route allowed `sessions:revoke` while the page immediately required more.

Fix level: **(a) one map, (c) where the map cannot see intent.** Generate the route permission from the endpoint policy for the CRUD screens. Two legal permission constants both type-check, so a type alias does not stop "I picked `users:write` because the next file did." The generated map fails the build when the UI constant and the endpoint policy differ. Judgment remains for screens that combine several permissions. That judgment is the only docs row for this class.

### 12. A container runs as root

Count: 2. Severity: high.

- PR #109, `3ebabea`. Admin Web nginx started as root.
- PR #419, `b96d9c3`. Root nginx user again, after the UI move. **Measured** today: Api and DbMigrator use `USER app`, Management Web uses `USER nginx`.

Fix level: **(b).** A Dockerfile check that the last `USER` is not root, and that nginx images contain `USER nginx`. Architecture types do not see Dockerfiles. The check names the expected user.

### 13. Docs name files or types that are not there

Count: 3. Severity: medium. This is why agent rules are last.

- PR #68, `4db9b45`. Review: production docs pointed at `deploy/open-identity-stack`, which was not in the repo.
- PR #100, `74bd1ab`. Review: skill files pointed at spec-kit scripts the repo did not contain.
- PR #141, `f91c837`. Review: the transaction note required `IApplicationPermissionTransactionRunner`, which did not exist. The PR template still asks reviewers to notice no-op transactions. The type was never added.

Fix level: **(d) only after the check exists.** Delete or rewrite a doc that names a missing type in the same change that deletes the type. Do not add a new instruction file to fix classes 1–12.

Not a class: the NuGet `System.Security.Cryptography.Pkcs` / `Xml` 10.0.12 audit failure. It happened once in git (`29e99f8`, PR #545) and again as an environment workaround (`NuGetAudit=false`). One pair is not yet a repeated product bug. The pin in `Directory.Packages.props` is the fix. Leave it.

Audit and transactions are not given their own class number because the two recorded policies disagree. They are an owner decision below. The incidents are PR #522 (denial audit must survive rollback, and consent plus audit must commit together) and PR #469 / #465 (revision capture and the mutation must be one transaction).

## Chosen enforcement

Order used above: architecture, then types or a lint that names the file to use, then behavior tests, then docs.

| Rule the agent would otherwise copy wrong | What enforces it | Level |
| --- | --- | --- |
| Admin HTTP calls a use case or a repository it found nearby | Api cannot reference repository interfaces or `I*UseCase`. It can reference `I*Admin` only. | (a) `internal` + architecture test |
| A second AppHost or a second `AddApplication` | One project, one composition call, test fails on a second hit | (a) |
| A second OpenAPI YAML or a permission string typed by hand | One YAML tree. Generated TS constants. Routes import them. | (a) |
| A second error mapper | One `ErrorResultMapper.ToErrorResult`. The throwaway check already fails on the private method at `ApplicationPermissionsApi.cs:686`. | (a) delete, (b) the check |
| Checkbox via `CheckAsync`, or `WaitForTimeoutAsync` | Source check. Failure text names click-and-assert. | (b) |
| `string.Equals` / `Contains` without `StringComparison` | Analyzer names `StringComparison.Ordinal` or the normalized column | (b) |
| Dockerfile ends as root | CI names `USER app` or `USER nginx` | (b) |
| Test with no assertion | Existing S2699 | (c) |
| Token path resolves roles twice | One snapshot type, plus a query-count test | (a) + (c) |
| UI permission constant ≠ endpoint policy | Generated map for CRUD. Docs only for multi-permission screens. | (a), docs for the exception |
| Coverage gate ignores a module | One script. Fixture with an empty module must fail. | (b) |
| Fresh authentication window, audit versus rollback, release-train intent | `AGENTS.md` table points at the row above. Prose only for these three. | (d) |

`AGENTS.md` gets that table in the last phase, after each check exists. Until then the table lives here. A rule without a check is how #141 and #465 happened.

## Two whole shapes

### Candidate A. One workflow type inside the current four projects

Keep SharedKernel, Domain, Application, Infrastructure, Api, AppHost, DbMigrator, Management Web, and `admin-api-client`.

Public surface an API file can use:

```csharp
group.MapPost("/users", async (CreateUserBody body, IUsersAdmin users, CancellationToken ct) =>
{
    Result<UserDetails> result = await users.CreateAsync(body.ToCommand(), ct);
    return result.ToHttpResult();
});

public interface IUsersAdmin
{
    Task<Result<UserDetails>> CreateAsync(CreateUser command, CancellationToken cancellationToken);
    Task<Result<UserDetails>> GetAsync(UserId id, CancellationToken cancellationToken);
    Task<Result<Page<UserSummary>>> ListAsync(ListUsers query, CancellationToken cancellationToken);
}
```

`ICreateUserUseCase` and `IUserRepository` are `internal`. Infrastructure and tests see them through `InternalsVisibleTo`. `ToHttpResult` is the only error function. `IApplicationsAdminWorkflow` is the existing version of this shape. Reads move onto the same interface so there is not a query-handler twin.

### Candidate B. One project per aggregate, public method is `MapUsers`

Each feature assembly exposes only:

```csharp
public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsers(this IEndpointRouteBuilder app) { /* ... */ }
}
```

Use cases and repositories are internal to that assembly. Api cannot name them. This hides more per project than candidate A.

### Screen

| Red flag | Candidate A | Candidate B |
| --- | --- | --- |
| Shared mutable state | Unchanged. Static permission catalogs stay immutable. Module-level token handlers in `ManagementWeb/src/lib/api.ts` stay a frontend problem, not a reason to split assemblies. | Same. |
| Multiple ways | Removes the use-case versus workflow split if every aggregate gets one `I*Admin`. | Each feature can still invent an internal use-case style. Agents copy the nearest feature. |
| Hand-synced lists | Program.cs lists workflow registrations. One list. | Program.cs, the migrator, and the solution file each list every feature project. That is the list class we are removing. |
| Hidden coupling | Ports stay in Application, so the coupling is visible. | Feature projects hide coupling until a shared kernel of "just this DTO" appears. **Guess:** that shared kernel shows up on the second feature. |
| Escape hatches | `Result<T>` stays. No `object` command bags. | A `MapUsers` that still takes `IUserRepository` as a parameter reopens class 1. The public signature must not. |
| Near-duplicate examples | One workflow file to copy. | Eight similar `.csproj` trees to copy. That is how Admin Web and Management Web both existed. |

Interface depth: candidate B's Api surface is one method per aggregate. Candidate A's Api surface is one interface per aggregate. B hides the method bodies one assembly further. A hides the 39 use-case interfaces without a new copyable project. **Inferred:** A is the smaller public surface that does not add a hand-synced project list. B looks smaller only at the `using` line, and it multiplies templates.

### Synthesis

Use candidate A.

Applications already proved the workflow shape. Users are the template agents copy, so Users change first, then roles, groups, and sessions. Application permissions stop taking maintenance use cases beside the registry workflow. Interactive sign-in (account, authorize, logout, cookie validation) moves behind `IInteractiveSignIn` in a later phase. Those controllers are the other template agents copy (`AuthorizationController` takes `IUserRepository` today). **Measured:** repository parameters also sit in `PublicProfilesApi` (three lines) and `SessionsApi` (one line), and in five Authentication files.

Do not add MediatR, AutoMapper, Newtonsoft, or Swashbuckle. Do not add a feature project per aggregate.

Error mapping: `ErrorResponse` is `(Error, Message)`. The private permissions mapper returns the same two fields for not-found, conflict, and bad request. Forbidden differs: Problem Details with `title` set to the error code, versus a bare 403 from `ErrorResultMapper` for a generic forbidden code (**measured**). Unifying them changes that response. See owner decisions.

Permission catalog:

```ts
// generated from Permissions.cs, not hand-edited
export const usersRead = "users:read" as const;
```

Routes and buttons import that module. The 9 JSON cases stay and keep running on both sides.

## Prototype results

Throwaway branch `cursor/throwaway-agent-shape-checks-637e`, commit `ee0ad79`. Not pushed. Not for review. The check file is not on this PR.

**Measured** on that branch, `dotnet test --project tests/OpenIdentityStack.Architecture.Tests`:

- Wall 1.66 s. Test duration 501 ms. Total 10, failed 3, succeeded 7, exit 2.
- The 7 successes are the existing layering and frontend-topology tests plus the new sleep check, which is clean on the current tree.
- `E2E_DoesNotCallPlaywrightLocatorCheckAsync` failed on `AdministrativeAccessManagementTests.cs:68`.
- `Api_HasASingleErrorResultMapper` failed on `ApplicationPermissionsApi.cs:686`.
- `AdminMinimalApis_DoNotInjectRepositories` failed on `PublicProfilesApi.cs` lines 29, 43, and 115, and `SessionsApi.cs` line 106.
- The first compile of the spike failed in 1.17 s (`CS0266`, missing `ToArray`). The next run compiled and produced the failures above in 1.87 s wall. The sleep check initially matched its own source text. After excluding the architecture project, it passed.

**Measured** historical sleep needle, `git grep WaitForTimeoutAsync b5f1922^ -- tests/**/*.cs`: 7 hits. The current-tree check does not see them because PR #200 deleted that test project.

**Measured** warm runs, already-built test DLLs, no solution build:

- Domain tests: 509 passed, duration 700 ms, wall 4 s.
- Permission matching class: 22 passed, duration 643 ms, wall 2.83 s.
- Permission semantics Vitest: 11 passed, duration 180 ms, wall 0.77 s.

**Not measured:** a cold `dotnet build` of `OpenIdentityStack.slnx`, Sonar S2699, the coverage-gate fixture, and a `StringComparison` analyzer. **Guess:** a cold solution build is several minutes. The NuGet-pin transcript summary reported CI `build-test` around 6 to 8 minutes. That number was not re-checked against Actions in this session.

The spike answers three questions. A source check can fail on the live `CheckAsync` mistake. The same style of check can see the second error mapper and the admin repository injections. The sleep check is quiet on today's tree and would have failed on the pre-#200 Admin Web tests. It does not answer whether `internal` use cases compile. **Inferred** from the project references: Api references Application, so `internal` interfaces disappear from Api as soon as `InternalsVisibleTo` omits Api. That compile is phase 5, not this PR.

## Phased execution

Each phase is its own PR. Do not start until this plan is accepted. Rollback of any phase is reverting that PR. Do not combine a behavior change with a move.

1. **Land the three checks red, then make them green.** Add the throwaway scans, minus the self-match, as real architecture tests. CI fails on line 68, on the private `ToErrorResult`, and on the four repository lines. Then fix those call sites without changing response bodies: click the checkbox and assert state; call `ErrorResultMapper` only where the body already matches `ErrorResponse`; leave forbidden behavior for phase 2. Repository injections in public profiles and sessions move behind existing query handlers, still public. Risk: E2E click does not toggle the Radix checkbox. Rollback: revert. The sleep check lands in this phase already green, with a comment that points at the 7 historical lines.

2. **One forbidden body.** Owner decision 1. Contract test the permissions API error before and after. Risk: a client parsed Problem Details `title`. Rollback: revert the mapper call.

3. **Delete the second OpenAPI YAML.** Point `GetOpenApiContractPaths` at `contracts/openapi` only. Risk: a test depended on the copy's trailing newline. **Inferred** low. Rollback: restore the file.

4. **Generate permission constants.** Routes import the generated module. CI fails if the file is stale. Keep the 9 JSON cases. Risk: a placeholder string gets generated as a real permission. The generator reads `Permissions.cs` constants only.

5. **Users workflow, and hide Users use cases.** Api Users code may name `IUsersAdmin` only. An architecture test fails otherwise. InternalsVisibleTo for Infrastructure and tests. Risk: a missed DI registration fails at startup, not at compile. Cover with one Api.Tests create-user test. Rollback: make the interfaces public again.

6. **Roles, groups, sessions admin.** Same test, one aggregate per PR.

7. **Applications and application permissions.** Hide lifecycle use cases behind the workflow that already exists. Stop injecting maintenance use cases next to the registry workflow. Risk: the architecture test `ApplicationPermissionsApiArchitectureTests` encodes today's split and must change in the same PR.

8. **Interactive sign-in workflow.** Move repository use out of Authentication. Risk: cookie validation and logout behavior. This phase is behavior-preserving and needs the existing auth integration tests. It does not finish `id_token_hint`. Rollback: revert.

9. **Snapshot owner on the token path.** One type returns effective permissions. A test fails if authorize or refresh resolves them twice. Risk: a behavior change in group-versus-role precedence. The #477 message says name-over-GUID precedence must stay. Assert that in the same test.

10. **Query-count and coverage fixtures.** Prove the token test fails when a second query is forced. Prove the coverage gate fails on an empty module. Then turn them on.

11. **Dockerfile user check, and the string-comparison analyzer, each on a past file.** The analyzer's first PR must show a failure on a two-argument `Equals` or `Contains` copied from history, then the current tree goes green. If the analyzer is too noisy, narrow it to Domain and Application identifier code. Do not weaken it with a blanket suppression.

12. **Write the rule table into `AGENTS.md`.** Delete checklist lines that now have a check. Keep the three judgment rows. Run `mkdocs build --strict` if a published doc changed.

Out of scope: proof-based identity recovery, logout `id_token_hint`, and a provider-id change on the wire. ADR 0005 and `DEFERRED-BACKEND-REMEDIATION-ITEMS.md` already defer those.

## Owner decisions

No experiment settles these. Defaults are what the next PRs should do if you do not answer.

1. **Forbidden errors on the application-permissions API.** Default: use `ErrorResultMapper`, and publish the body change with Management Web in one release (ADR 0003). The other choice is to teach the shared mapper the Problem Details shape and keep today's permissions responses.

2. **Audit versus rollback.** The reviews want both policies. Default: a denied security attempt commits its audit even when the mutation rolls back (PR #522 denial case). A successful consent or admin mutation commits the audit in the same transaction as the row (PR #522 consent case, PR #465 revision capture). Do not pick one global rule.

3. **Reads on the same interface as writes.** Default: one `IUsersAdmin` with create, get, and list. A separate query interface is a second type to inject.

4. **Feature projects.** Default: no. Stay in the four layers. `internal` is the hiding mechanism.

5. **Logout `id_token_hint` and provider id on the wire.** Default: leave both deferred. An internal provider value object may land later without an OpenAPI change.

6. **Five-minute fresh authentication.** Default: leave the window alone. It is a product rule in ADR 0005.

7. **Multi-permission screens.** Default: the generated map covers single-permission CRUD. Screens that need several permissions stay explicit and get a behavior test, not a new framework.
