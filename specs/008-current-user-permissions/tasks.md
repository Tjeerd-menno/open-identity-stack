# Tasks: Current User Permissions

**Input**: Design documents from `specs/008-current-user-permissions/`

**Prerequisites**: `spec.md`, `plan.md`, `contracts/current-user.openapi.yaml`

**Tests**: Required before implementation. This is a security-sensitive cross-layer bugfix with externally consumed API behavior.

## Format

`[ID] [P?] [Slice] Description`

- **[P]**: Can run in parallel because it touches a different file or has no dependency on another pending task.
- **[Slice]**: `S1` backend endpoint, `S2` shared client/contract, `S3` Management Web integration, `S4` docs.

## Phase 1: Backend Current User Endpoint

### Tests

- [X] T001 [P] [S1] Add API unit tests for current-user response extraction from a `ClaimsPrincipal`, including subject, display fallbacks, email, permission claim collection, case-insensitive deduplication, wildcard preservation, and scope exclusion.
- [X] T002 [P] [S1] Add API unit/integration coverage proving missing `sub`/name identifier returns `401`.
- [X] T003 [P] [S1] Add separate current-user route mapping test for `GET /api/me`, endpoint name, tag, HTTP method, and authenticated authorization metadata without a named permission policy.

### Implementation

- [X] T010 [S1] Add `src/OpenIdentityStack.Api/CurrentUser/CurrentUserApi.cs` with `MapCurrentUserApi`.
- [X] T011 [S1] Map `app.MapCurrentUserApi()` from `src/OpenIdentityStack.Api/Program.cs`.
- [X] T012 [S1] Implement permission extraction from explicit `permission` and `permissions` claims only.
- [X] T013 [S1] Log anomalous missing-subject failures without logging every successful `/api/me` call.

## Phase 2: Shared Client And Contract

### Tests

- [X] T020 [P] [S2] Add contract tests for `GET /api/me`, `CurrentUserResponse`, and `401`.
- [X] T021 [P] [S2] Add admin-api-client tests for `createCurrentUserContract().getCurrentUser()`.

### Implementation

- [X] T030 [S2] Add `specs/008-current-user-permissions/contracts/current-user.openapi.yaml` to contract test inputs as needed.
- [X] T031 [S2] Add `CurrentUserResponse` and `createCurrentUserContract` to `src/frontend-packages/admin-api-client`.
- [X] T032 [S2] Export current-user types and contract from `src/frontend-packages/admin-api-client/src/index.ts`.
- [X] T033 [S2] Wire `api.currentUser` in `src/OpenIdentityStack.ManagementWeb/src/lib/api.ts`.

## Phase 3: Management Web Auth Integration

### Tests

- [X] T040 [P] [S3] Add Management Web auth provider test using an opaque access token and mocked `/api/me` response, asserting returned permissions drive auth state.
- [X] T041 [P] [S3] Add Management Web test proving auth remains loading until `/api/me` resolves.
- [X] T042 [P] [S3] Add Management Web test proving `401` from `/api/me` triggers the existing unauthorized/logout path.
- [X] T043 [P] [S3] Add Management Web test proving non-401 `/api/me` failure surfaces an explicit auth/authorization state error.
- [X] T044 [P] [S3] Add Management Web test proving a changed access-token value refetches `/api/me`.

### Implementation

- [X] T050 [S3] Update `src/OpenIdentityStack.ManagementWeb/src/lib/auth.tsx` to fetch current-user data after OIDC user load and on access-token changes.
- [X] T051 [S3] Remove Management Web production dependency on `extractGrantedPermissions`.
- [X] T052 [S3] Preserve permission-based route/action gating through `auth.permissions`.
- [X] T053 [S3] Keep mock/E2E auth provider behavior stable for existing Playwright tests.

## Phase 4: Documentation

- [X] T060 [S4] Add ADR `docs/adr/0004-management-web-opaque-access-token-permissions.md`.
- [X] T061 [S4] Update `docs/management-web.md` authorization section to document `/api/me`.
- [X] T062 [S4] Update `specs/007-management-web/spec.md` with supersession notes for token-decoding requirements.

## Phase 5: Verification

- [X] T070 Run `dotnet build OpenIdentityStack.slnx --no-restore`.
- [X] T071 Run focused API unit tests for current-user endpoint.
- [X] T072 Run focused contract tests for `/api/me`.
- [X] T073 Run `cd src/OpenIdentityStack.ManagementWeb; npm run build && npm run lint && npm test`.
- [X] T074 Run focused Management Web tests for auth/current-user behavior.
- [X] T075 Run `git diff --check`.
