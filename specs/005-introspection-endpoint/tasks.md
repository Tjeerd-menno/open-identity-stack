# Tasks: OIDC Token Introspection Endpoint

**Input**: Design documents from `specs/005-introspection-endpoint/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/introspection.openapi.yaml`, `quickstart.md`

**Tests**: Required by the feature specification and constitution. Write or update tests before each story's implementation tasks.

**Organization**: Tasks are grouped by user story so each story can be implemented and verified independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it touches a different file or has no dependency on another pending task.
- **[Story]**: User story identifier (`US1`, `US2`, `US3`).
- Include exact file paths in every task description.

## Phase 1: Setup

**Purpose**: Confirm the existing project and feature context before changing behavior.

- [X] T001 Confirm no new package, storage, or migration work is required by reviewing `specs/005-introspection-endpoint/plan.md`, `specs/005-introspection-endpoint/research.md`, and `Directory.Packages.props`.
- [X] T002 [P] Verify existing OpenIddict client registration and test seeding can grant introspection endpoint permissions in `src/OpenIdentityStack.Infrastructure/Identity/OpenIddictClientApplicationRegistrar.cs` and `tests/TestSeedHelpers/OpenIdentityStackTestSeeder.cs`.

## Phase 2: Foundational

**Purpose**: Establish shared endpoint infrastructure required by all stories.

- [X] T003 [P] Add the `IntrospectionEndpoint` request rate limiting policy in `src/OpenIdentityStack.Api/Program.cs`.
- [X] T004 [P] Register the OpenIddict introspection response enrichment handler in `src/OpenIdentityStack.Infrastructure/Identity/OpenIddictSetup.cs`.
- [X] T005 [P] Update test client seeding to include OpenIddict token, introspection, and revocation endpoint permissions in `tests/TestSeedHelpers/OpenIdentityStackTestSeeder.cs`.

## Phase 3: User Story 1 - Authenticated APIs introspect tokens (Priority: P1)

**Goal**: Authenticated API callers can submit an access token to `/connect/introspect` and receive active state, subject, and authorization metadata.

**Independent Test**: Submit an introspection request as an authenticated API client and verify `active`, `sub`, and permissions are returned; submit as an unauthenticated caller and verify metadata is not disclosed.

### Tests for User Story 1

- [X] T006 [P] [US1] Add route coverage for `POST /connect/introspect` in `tests/OpenIdentityStack.Api.UnitTests/Endpoints/OidcControllerRouteTests.cs`.
- [X] T007 [P] [US1] Add unauthenticated introspection rejection coverage in `tests/OpenIdentityStack.Api.Tests/Authentication/AuthorizationControllerTests.cs`.

### Implementation for User Story 1

- [X] T008 [US1] Add the `Introspect` action at `/connect/introspect` in `src/OpenIdentityStack.Api/Authentication/AuthorizationController.cs`.
- [X] T009 [US1] Ensure the introspection action returns OpenIddict challenge behavior for invalid authentication and an active response shape with `active`, optional `sub`, and `permissions` in `src/OpenIdentityStack.Api/Authentication/AuthorizationController.cs`.

**Checkpoint**: User Story 1 is the MVP and should pass route and unauthenticated rejection verification independently.

## Phase 4: User Story 2 - Permissions are filtered by requesting API (Priority: P2)

**Goal**: The requesting API receives only permissions relevant to its own service boundary.

**Independent Test**: Introspect a token for a user with permissions in multiple service namespaces and verify the caller receives only permissions matching its client/service identifier.

### Tests for User Story 2

- [X] T010 [P] [US2] Add controller/API coverage for caller-filtered permissions in `tests/OpenIdentityStack.Api.Tests/Authentication/AuthorizationControllerTests.cs`.
- [X] T011 [P] [US2] Add infrastructure handler coverage for filtering mixed service permissions in `tests/OpenIdentityStack.Infrastructure.Tests/Identity/IntrospectionPermissionsHandlerTests.cs`.

### Implementation for User Story 2

- [X] T012 [US2] Implement the scoped OpenIddict introspection enrichment handler in `src/OpenIdentityStack.Infrastructure/Identity/IntrospectionPermissionsHandler.cs`.
- [X] T013 [US2] Filter handler permissions by requesting API client id, remove duplicates, and exclude unrelated service permissions in `src/OpenIdentityStack.Infrastructure/Identity/IntrospectionPermissionsHandler.cs`.

**Checkpoint**: User Story 2 should pass independently by proving unrelated API permissions are absent.

## Phase 5: User Story 3 - Authorization changes are reflected quickly (Priority: P3)

**Goal**: IAM resolves current user permissions at introspection time so role changes affect the next successful introspection response.

**Independent Test**: Change or simulate current user role permissions after token issuance and verify introspection reflects the current permission set rather than stale token permission claims.

### Tests for User Story 3

- [X] T014 [P] [US3] Add handler coverage proving current role permissions override stale token permission claims in `tests/OpenIdentityStack.Infrastructure.Tests/Identity/IntrospectionPermissionsHandlerTests.cs`.
- [X] T015 [P] [US3] Add controller/API coverage for fresh role permission resolution in `tests/OpenIdentityStack.Api.Tests/Authentication/AuthorizationControllerTests.cs`.

### Implementation for User Story 3

- [X] T016 [US3] Resolve GUID user subject permissions through `IGetUserEffectiveRolesQueryHandler` in `src/OpenIdentityStack.Infrastructure/Identity/IntrospectionPermissionsHandler.cs`.
- [X] T017 [US3] Fall back to token `permission` and `permissions` claims only for non-user subjects or unresolved role data in `src/OpenIdentityStack.Infrastructure/Identity/IntrospectionPermissionsHandler.cs`.

**Checkpoint**: User Story 3 should pass independently by proving fresh role data determines returned permissions for user tokens.

## Phase 6: Polish & Cross-Cutting Verification

**Purpose**: Validate contracts, docs, formatting, and the focused test matrix.

- [X] T018 [P] Verify the endpoint contract includes authenticated `POST /connect/introspect`, `active`, optional `sub`, `permissions`, `401`, and `429` responses in `specs/005-introspection-endpoint/contracts/introspection.openapi.yaml`.
- [X] T019 [P] Verify quickstart build, focused test commands, and manual smoke shape in `specs/005-introspection-endpoint/quickstart.md`.
- [X] T020 Run `dotnet build OpenIdentityStack.slnx --no-restore` from the repository root.
- [X] T021 Run the focused route, API, and infrastructure tests listed in `specs/005-introspection-endpoint/quickstart.md`.
- [X] T022 Run `git diff --check` from the repository root.

## Dependencies & Execution Order

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup completion.
- **US1 (Phase 3)**: Depends on Foundational completion; this is the MVP.
- **US2 (Phase 4)**: Depends on Foundational completion and can begin after the handler registration shape is known.
- **US3 (Phase 5)**: Depends on Foundational completion and shares handler work with US2.
- **Polish (Phase 6)**: Depends on all desired user stories being complete.

## User Story Dependencies

- **US1**: Independent after Foundational; delivers the first usable introspection endpoint.
- **US2**: Independent after Foundational; can be implemented with handler-focused filtering tests.
- **US3**: Independent after Foundational, but should reuse the handler introduced for US2 when both stories are in scope.

## Parallel Opportunities

- T002 can run in parallel with T001.
- T003, T004, and T005 can run in parallel because they touch different files.
- T006 and T007 can run in parallel within US1.
- T010 and T011 can run in parallel within US2.
- T014 and T015 can run in parallel within US3.
- T018 and T019 can run in parallel during polish.

## Implementation Strategy

### MVP First

1. Complete Phase 1 and Phase 2.
2. Complete US1 tests T006 and T007.
3. Complete US1 implementation T008 and T009.
4. Validate route and unauthenticated rejection behavior before adding permission filtering or freshness.

### Incremental Delivery

1. Deliver US1 as the minimal OAuth introspection surface.
2. Add US2 to constrain permission disclosure by requesting API.
3. Add US3 to make introspection reflect current role permissions.
4. Finish with contract, quickstart, build, focused tests, and whitespace verification.

### Notes

- No new NuGet packages, database tables, migrations, deployment assets, or Management Web changes are expected.
- Keep OpenIddict responsible for caller authentication and token activity semantics.
- Keep permission enrichment in Infrastructure and HTTP route/controller tests in Api test projects.

