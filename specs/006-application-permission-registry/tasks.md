# Tasks: Application Permission Registry

**Input**: Design documents from `specs/006-application-permission-registry/`

**Prerequisites**: `spec.md`, `plan.md`, `data-model.md`, `quickstart.md`, `contracts/application-permission-registry.openapi.yaml`, `decision-log.md`

**Tests**: Required before implementation in each slice. Security-sensitive externally visible behavior requires API/integration coverage. Project-owned `/api/admin/*` changes require contract tests. Backend and Management Web ship together per slice.

## Format

`[ID] [P?] [Slice] Description`

- **[P]**: Can run in parallel because it touches a different file or has no dependency on another pending task.
- **[Slice]**: `S1` through `S5`.
- Exact implementation task decomposition must be refined before execution.

## Phase 1: Setup And Contract Finalization

- [X] T001 Finalize exact OpenAPI request/response schemas in `specs/006-application-permission-registry/contracts/application-permission-registry.openapi.yaml`.
- [X] T002 [P] Add platform permission catalog contract outline for `GET /api/admin/permissions/platform`.
- [X] T003 [P] Review current application-permission implementation and identify code to keep, rename, replace, or remove.
- [X] T004 Confirm clean database/destructive reset expectation in developer/deployment notes for this alpha feature.

## Phase 2: Slice 1 - Application Registration And Inline Manifest Management

### Tests

- [X] T010 [P] [S1] Add manifest validation unit tests for schema version, SemVer, application id, permission key shape, duplicate keys, and manifestBaseUrl validation.
- [X] T011 [P] [S1] Add application/use-case tests for create, list/detail, non-destructive target-specific update, same/older version conflict, and destructive omission rejection.
- [X] T012 [P] [S1] Add authorization tests for user owner, group owner, delegated maintainer, group maintainer, unrelated writer, and admin override.
- [X] T013 [P] [S1] Add contract tests for slice-1 `/api/admin/application-permissions/applications*` endpoints.
- [X] T014 [P] [S1] Add Management Web unit/API-client tests for manifest editor validation and API errors.
- [X] T015 [S1] Add Management Web E2E for create from inline manifest, detail view, newer non-destructive update, and same/older version validation.

### Implementation

- [X] T020 [S1] Implement domain/application models for current applications, current permissions, owners, maintainers, manifest metadata, status, and concurrency.
- [X] T021 [S1] Implement EF persistence for slice-1 current application/permission/maintainer data.
- [X] T022 [S1] Implement manifest parsing/validation and SemVer comparison.
- [X] T023 [S1] Implement ownership/maintainer authorization including group membership resolution.
- [X] T024 [S1] Implement inline create, list, detail, target-specific preview/apply, metadata update, ownership transfer, maintainer add/remove, enable, and disable endpoints.
- [X] T025 [S1] Implement Management Web `Application Permissions` nav, list, detail, create, raw JSON editor, structured editor, maintainer management, ownership transfer, enable/disable, and manifest update flows.

## Phase 3: Slice 2 - Role Picker, Assignment Validation, Broad Grants, And Emission

### Tests

- [X] T030 [P] [S2] Add `Permissions.Matches` multi-segment wildcard unit tests.
- [X] T031 [P] [S2] Add assignability/classifier tests for platform permissions, dynamic concrete permissions, dynamic wildcards, disabled applications, invalid strings, tombstones, deleted applications, and acknowledgement requirements.
- [X] T032 [P] [S2] Add role API integration tests for create/add/set/remove permission validation and broad-grant acknowledgement.
- [X] T033 [P] [S2] Add contract tests for role request acknowledgement fields, broad-grant conflicts, platform catalog, and dynamic catalog wildcard entries.
- [X] T034 [P] [S2] Add token issuance tests for concrete-only dynamic/platform expansion and fail-closed behavior.
- [X] T035 [P] [S2] Add introspection tests for concrete-only dynamic expansion, requesting client filtering, and platform permission exclusion.
- [X] T036 [S2] Add Management Web E2E for dynamic wildcard assignment and one platform broad-grant acknowledgement flow.

### Implementation

- [X] T040 [S2] Implement platform permission catalog metadata without changing concrete policy registration semantics.
- [X] T041 [S2] Implement dynamic catalog derived aggregate wildcard entries and assignable flags.
- [X] T042 [S2] Implement central permission classifier/assignability service.
- [X] T043 [S2] Update role create/add/set request handling with `acknowledgeWildcardGrant`.
- [X] T044 [S2] Update `Permissions.Matches` for multi-segment prefix wildcards.
- [X] T045 [S2] Implement token/introspection dynamic permission validation and wildcard expansion to concrete permissions only.
- [X] T046 [S2] Implement Management Web unified role picker with source/kind display and broad-grant acknowledgement.

## Phase 4: Slice 3 - Destructive Manifest/Delete Workflows

### Tests

- [X] T050 [P] [S3] Add application tests for destructive manifest omissions, permission delete, application delete, tombstones, exact assignment removals, wildcard collapse removals, and rollback on failure.
- [X] T051 [P] [S3] Add infrastructure transaction tests covering registry, assignment, manifest version, and audit atomicity.
- [X] T052 [P] [S3] Add API integration tests for permission deletion impact/apply and application deletion impact/apply.
- [X] T053 [P] [S3] Add contract tests for destructive preview and result DTOs.
- [X] T054 [S3] Add Management Web E2E for one representative destructive manifest omission preview/apply flow.

### Implementation

- [X] T060 [S3] Add tombstone fields and persistence for removed permissions and deleted applications.
- [X] T061 [S3] Implement `IPermissionAssignmentStore` with initial role assignment store.
- [X] T062 [S3] Implement transactional unit-of-work for registry mutation, assignment cleanup, manifest version update, and audit writes.
- [X] T063 [S3] Implement destructive manifest omission, permission delete, application delete, deletion-impact endpoints, dependency impact models, and audit payloads.
- [X] T064 [S3] Implement Management Web destructive preview/confirmation/result flows.

## Phase 5: Slice 4 - Remote Import And Trust Flow

### Tests

- [X] T070 [P] [S4] Add remote fetch validation tests for trusted base URL, wrong app id, redirects, content type, response size, timeout, HTTPS/dev exceptions, and duplicate base URL.
- [X] T071 [P] [S4] Add API integration and contract tests for remote preview/apply.
- [X] T072 [S4] Add Management Web E2E for preview/apply from controlled local fixture endpoint.

### Implementation

- [X] T080 [S4] Implement remote manifest fetch service with no redirects, trusted URL derivation, content-type/size/timeout limits, and strict application id matching.
- [X] T081 [S4] Implement target-specific remote preview/apply endpoints.
- [X] T082 [S4] Implement Management Web remote import UI.

## Phase 6: Slice 5 - Tombstone History, Replacement Guidance, And Diagnostics

### Tests

- [X] T090 [P] [S5] Add tests for tombstone history reads, replacement guidance validation, and admin authorization.
- [X] T091 [P] [S5] Add diagnostics tests for malformed, unknown, orphaned, deleted, and collapsed wildcard assignment data.
- [X] T092 [P] [S5] Add contract tests for history/replacement/diagnostic endpoints.

### Implementation

- [X] T100 [S5] Implement explicit tombstone/history endpoints.
- [X] T101 [S5] Implement removed-permission replacement guidance endpoint.
- [X] T102 [S5] Implement integrity diagnostics and Management Web diagnostics/remediation view.

## Phase 7: Cleanup And Verification

- [X] T110 Run `dotnet build OpenIdentityStack.slnx --no-restore`.
- [X] T111 Run focused .NET tests for all completed slices.
- [X] T112 Run Management Web `npm run build`, `npm run lint`, and `npm test`.
- [X] T113 Run focused Management Web E2E for completed slices.
- [X] T114 Run `git diff --check`.
- [X] T115 Archive or clearly mark non-authoritative Copilot application-permission registry specs after `006` is accepted.

