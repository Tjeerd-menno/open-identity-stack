# Shared Admin API Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deepen `src/frontend-packages/admin-api-client` from a fetch-like transport module into the shared Admin API contract used by AdminWeb and Management Web.

**Architecture:** Keep UI-specific auth and runtime configuration in each UI adapter, but move Admin API domain endpoint functions, shared request types, response types, path construction, and response normalization into the shared package. Migrate one domain at a time so each slice is testable and releasable.

**Tech Stack:** TypeScript 6, Vite, Vitest, React Query callers, `@openidentitystack/admin-api-client`, AdminWeb, Management Web.

---

## File Structure

- Create: `src/frontend-packages/admin-api-client/package.json` so the shared module has a real package seam.
- Modify: `src/frontend-packages/admin-api-client/src/index.ts` to keep transport exports and re-export domain contracts.
- Create: `src/frontend-packages/admin-api-client/src/users.ts` and `src/frontend-packages/admin-api-client/src/users.test.ts`.
- Create: `src/frontend-packages/admin-api-client/src/groups.ts` and `src/frontend-packages/admin-api-client/src/groups.test.ts`.
- Create: `src/frontend-packages/admin-api-client/src/applications.ts` and `src/frontend-packages/admin-api-client/src/applications.test.ts`.
- Create: `src/frontend-packages/admin-api-client/src/roles.ts`, `sessions.ts`, `providers.ts`, `settings.ts`, `audit-entries.ts`, `application-permissions.ts` with matching tests.
- Modify: `src/OpenIdentityStack.ManagementWeb/src/features/*/*-api.ts` to re-export shared contract functions instead of duplicating endpoint paths.
- Modify: `src/OpenIdentityStack.AdminWeb/src/features/*/api/*-api.ts` to re-export shared contract functions behind existing names where needed.
- Modify: `src/OpenIdentityStack.ManagementWeb/vite.config.ts`, `src/OpenIdentityStack.AdminWeb/vite.config.ts`, and TS configs only if the current alias cannot resolve the package cleanly.

## Task Breakdown

### Task 1: Give the shared client a real package seam

**Files:**
- Create: `src/frontend-packages/admin-api-client/package.json`
- Modify: `src/frontend-packages/admin-api-client/src/index.ts`
- Test: `src/frontend-packages/admin-api-client/src/index.test.ts`

- [ ] Add `package.json` with name `@openidentitystack/admin-api-client`, `type: module`, `private: true`, `main: ./src/index.ts`, `types: ./src/index.ts`, and script `test: vitest --run`.
- [ ] Run `cd src/frontend-packages/admin-api-client; npm test -- src/index.test.ts`.
- [ ] Keep existing transport tests passing before adding domain exports.
- [ ] Commit: `git add src/frontend-packages/admin-api-client && git commit -m "Add shared admin api client package seam"`.

### Task 2: Move Users contract into the shared client

**Files:**
- Create: `src/frontend-packages/admin-api-client/src/users.ts`
- Create: `src/frontend-packages/admin-api-client/src/users.test.ts`
- Modify: `src/frontend-packages/admin-api-client/src/index.ts`
- Modify: `src/OpenIdentityStack.ManagementWeb/src/features/users/users-api.ts`
- Modify: `src/OpenIdentityStack.AdminWeb/src/features/users/api/users-api.ts`

- [ ] Write shared tests for `getUsers`, `getUser`, `createUser`, `updateUser`, status changes, role assignment, groups, and upstream identities.
- [ ] Verify tests fail because `createUsersClient` or equivalent Users contract does not exist.
- [ ] Implement `users.ts` with one exported factory, `createUsersContract(client: AdminApiClient)`, and exported request/response types currently duplicated by both UIs.
- [ ] Preserve existing UI function names by delegating from each UI-local file to the shared Users contract.
- [ ] Run `cd src/frontend-packages/admin-api-client; npm test -- src/users.test.ts`.
- [ ] Run `cd src/OpenIdentityStack.ManagementWeb; npm test -- src/features/users/users-api.test.ts src/features/users/UsersPage.test.tsx`.
- [ ] Run `cd src/OpenIdentityStack.AdminWeb; npm test -- src/features/users/api/users-api.test.ts`.
- [ ] Commit: `git add src/frontend-packages/admin-api-client src/OpenIdentityStack.ManagementWeb/src/features/users src/OpenIdentityStack.AdminWeb/src/features/users && git commit -m "Move users admin api contract to shared client"`.

### Task 3: Move Groups contract into the shared client

**Files:**
- Create: `src/frontend-packages/admin-api-client/src/groups.ts`
- Create: `src/frontend-packages/admin-api-client/src/groups.test.ts`
- Modify: `src/OpenIdentityStack.ManagementWeb/src/features/groups/groups-api.ts`
- Modify: `src/OpenIdentityStack.AdminWeb/src/features/groups/api/groups-api.ts`

- [ ] Write tests for paged group list params, group CRUD, members, mappings, and mapping removal path.
- [ ] Implement `createGroupsContract(client)` and shared `Group`, `GroupMember`, `GroupMapping`, and request types.
- [ ] Replace URLSearchParams construction in AdminWeb with shared params handling.
- [ ] Run shared, Management Web, and AdminWeb group API tests.
- [ ] Commit: `git add src/frontend-packages/admin-api-client/src/groups.ts src/frontend-packages/admin-api-client/src/groups.test.ts src/OpenIdentityStack.ManagementWeb/src/features/groups src/OpenIdentityStack.AdminWeb/src/features/groups && git commit -m "Move groups admin api contract to shared client"`.

### Task 4: Move Applications and Application Permissions contracts

**Files:**
- Create: `src/frontend-packages/admin-api-client/src/applications.ts`
- Create: `src/frontend-packages/admin-api-client/src/application-permissions.ts`
- Create matching `*.test.ts` files
- Modify matching API files in both UIs

- [ ] Move Applications first: list/detail/profile policies/create/update/OAuth/lifecycle/delete/credentials.
- [ ] Assert one-time secret response shapes in shared tests.
- [ ] Move Application Permissions second: registered applications, manifest preview/apply/import, lifecycle, ownership, maintainers, catalog, history, diagnostics, dependency, replacement, deletion.
- [ ] Keep UI-local files as compatibility adapters until all callers are migrated.
- [ ] Run `npm test` slices for both UIs covering applications and application permissions.
- [ ] Commit separately for Applications and Application Permissions.

### Task 5: Move remaining read/write domains

**Files:**
- Create: `src/frontend-packages/admin-api-client/src/roles.ts`
- Create: `src/frontend-packages/admin-api-client/src/sessions.ts`
- Create: `src/frontend-packages/admin-api-client/src/providers.ts`
- Create: `src/frontend-packages/admin-api-client/src/settings.ts`
- Create: `src/frontend-packages/admin-api-client/src/audit-entries.ts`
- Modify matching UI API adapters

- [ ] Move Roles and platform permission catalog.
- [ ] Move Sessions and user-wide revoke.
- [ ] Move Providers and enable/disable lifecycle.
- [ ] Move Settings and authentication provider list.
- [ ] Move Audit Entries.
- [ ] Run focused UI tests after each domain migration.
- [ ] Commit each domain separately.

### Task 6: Remove duplicate contract tests that only assert URLs

**Files:**
- Modify: `src/OpenIdentityStack.ManagementWeb/src/features/*/*-api.test.ts`
- Modify: `src/OpenIdentityStack.AdminWeb/src/features/*/api/*-api.test.ts`
- Keep: shared package tests as the URL/request body source of truth

- [ ] Replace UI-local URL assertion tests with adapter smoke tests: “delegates through the shared contract and preserves exported function names.”
- [ ] Keep UI tests that cover UI-specific response adaptation.
- [ ] Run both full frontend test suites.
- [ ] Commit: `git add src/OpenIdentityStack.ManagementWeb src/OpenIdentityStack.AdminWeb src/frontend-packages/admin-api-client && git commit -m "Consolidate admin api contract tests"`.

## Validation

- `cd src/frontend-packages/admin-api-client; npm test`
- `cd src/OpenIdentityStack.ManagementWeb; npm run type-check && npm test`
- `cd src/OpenIdentityStack.AdminWeb; npm run type-check && npm test -- --run`
- `dotnet test --project tests/OpenIdentityStack.Contract.Tests/OpenIdentityStack.Contract.Tests.csproj`

## Rollout Notes

- This reinforces ADR-0003 and does not change backend routes.
- Keep UI adapters until all feature modules stop importing UI-local contract types.
- Stop after each domain if behavior drift appears; fix shared tests before continuing.
