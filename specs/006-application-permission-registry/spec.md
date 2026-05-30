# Feature Specification: Application Permission Registry

**Feature Branch**: `006-application-permission-registry`

**Created**: 2026-05-28

**Status**: Implementing

**Input**: Product decisions captured in [decision-log.md](decision-log.md). Promote the non-authoritative Copilot application-permission registry work into a clean numbered Spec Kit spec aligned with current implementation direction and latest product decisions.

## Governance And Precedence

- This numbered spec is authoritative once accepted.
- It supersedes earlier contradictory draft/planning text about service-permission routes, permission lifecycles, retired application states, arbitrary role permission strings, and Native AOT-driven endpoint constraints.
- `004-native-aot-backend` is postponed and non-authoritative for current implementation.
- `002-remove-banned-packages` remains a permanent architecture rule: no MediatR, no Swashbuckle, direct use-case/query-handler injection, Scalar/Microsoft OpenAPI.
- `003-test-coverage-improvement` remains a permanent quality bar with pragmatic, risk-based tests.
- The project is pre-1.0 alpha. This spec may require a clean database and does not guarantee migration compatibility with pre-existing role permission data.

## User Scenarios And Testing *(mandatory)*

### User Story 1 - Register and maintain application permission manifests (Priority: P1)

An application/resource API owner registers an application permission manifest so IAM can expose the resource's permissions to administrators without code changes.

**Why this priority**: Dynamic application permissions do not exist until the registry can store complete versioned manifests, ownership, maintainers, and current application state.

**Independent Test**: Create an application permission registration from an inline manifest, view it in AdminWeb, update it with a strictly newer non-destructive inline manifest, and verify same/older manifest versions are rejected.

**Acceptance Scenarios**:

1. **Given** a user with `application-permissions:write`, **When** they register a new application they own with a valid inline manifest, **Then** IAM stores the application, owner, current manifest version, and all current concrete permissions.
2. **Given** a current application and a newer manifest that only adds permissions or updates display metadata, **When** an owner, delegated maintainer, or admin applies it, **Then** IAM advances the manifest version and updates the current permission surface.
3. **Given** a current application and an inline manifest with the same or older SemVer version, **When** it is submitted, **Then** IAM rejects it with `409 Conflict`.
4. **Given** a slice-1 inline manifest update omits an existing permission, **When** it is submitted before destructive workflows are implemented, **Then** IAM rejects it with `409 DestructiveManifestChangeNotSupportedYet`.
5. **Given** an application has user or group owners and delegated maintainers, **When** current group members or users act on the application, **Then** authorization is evaluated from current IAM principal/group state.

---

### User Story 2 - Assign registered permissions and broad grants to roles (Priority: P1)

An administrator edits roles using platform permissions and dynamic application permissions from catalog data, with explicit acknowledgement for broad grants.

**Why this priority**: The registry only delivers value when registered permissions can be safely consumed by RBAC role workflows.

**Independent Test**: Register an application with multiple permissions, assign a concrete dynamic permission and an aggregate wildcard permission to a role through AdminWeb, confirm wildcard acknowledgement is required, and verify token/introspection permission emission contains concrete permissions only.

**Acceptance Scenarios**:

1. **Given** current application permissions exist, **When** an administrator opens role editing, **Then** AdminWeb shows platform permissions and application permissions in one picker while the backend keeps their catalog endpoints separate.
2. **Given** a permission aggregate has current concrete permissions, **When** the catalog is queried, **Then** IAM returns a derived `application:aggregate:*` wildcard entry marked as `kind: wildcard`.
3. **Given** a role mutation adds a platform wildcard, dynamic aggregate wildcard, or `*`, **When** the request omits `acknowledgeWildcardGrant`, **Then** IAM returns `409 RolePermissions.BroadGrantAcknowledgementRequired` with structured warning details.
4. **Given** a role already has a broad grant, **When** a replacement operation preserves it, **Then** IAM does not require re-acknowledgement.
5. **Given** a role contains wildcard grants, **When** token issuance or introspection emits permissions, **Then** wildcard strings are expanded to current concrete permissions and no wildcard string is emitted.
6. **Given** a dynamic permission is not scoped to the target resource/audience, **When** token issuance or introspection emits permissions, **Then** IAM omits that dynamic permission.

---

### User Story 3 - Apply destructive manifest and delete workflows safely (Priority: P2)

An application permission admin removes permissions or applications while IAM tombstones registry records and automatically removes affected role assignments transactionally.

**Why this priority**: Complete versioned manifests may omit permissions, and registry cleanup must keep role assignments consistent without leaving missing permission strings.

**Independent Test**: Apply a newer manifest that omits a permission with exact and wildcard role assignments, verify AdminWeb requires preview, and verify IAM tombstones the permission, removes exact role assignments, collapses wildcard assignments when the aggregate disappears, advances manifest version, and records audit details transactionally.

**Acceptance Scenarios**:

1. **Given** a newer complete manifest omits an existing permission, **When** an admin applies it, **Then** IAM soft-removes the permission, removes exact assignments, reports wildcard impacts, collapses aggregate wildcard assignments when needed, and commits all changes atomically.
2. **Given** a concrete permission is manually deleted, **When** the caller has `application-permissions:admin`, concurrency is current, and a reason is supplied, **Then** IAM performs the same tombstone and assignment cleanup semantics as manifest omission.
3. **Given** an application is deleted, **When** the caller has `application-permissions:admin`, concurrency is current, and a reason is supplied, **Then** IAM tombstones the application and current permissions, removes assignments in that namespace, and audits all changes.
4. **Given** AdminWeb performs a destructive operation, **When** the operator has not viewed impact preview, **Then** AdminWeb does not enable the final destructive action.
5. **Given** an assignment removal or audit write fails, **When** a destructive operation is in progress, **Then** the whole operation rolls back and the previous registry/assignment state remains.

---

### User Story 4 - Import manifests from trusted remote sources (Priority: P3)

An admin or authorized owner imports a newer manifest from the application's registered trusted `.well-known/permissions` endpoint.

**Why this priority**: Service-owned manifests should be operationally importable from deployed resource APIs without copying JSON manually.

**Independent Test**: Configure a trusted manifest base URL, preview a remote import from a controlled local fixture endpoint, apply the newer manifest, and verify the imported result matches the preview and audit trail.

**Acceptance Scenarios**:

1. **Given** an application has a trusted `manifestBaseUrl`, **When** remote import preview runs, **Then** IAM fetches `{manifestBaseUrl}/.well-known/permissions`, validates the manifest, and returns the same impact model as inline preview without side effects.
2. **Given** a fetched manifest has an `application.id` that differs from the registered application identifier, **When** remote import is attempted, **Then** IAM rejects it and audits the failure.
3. **Given** the remote endpoint redirects, returns an unsupported content type, exceeds size limits, times out, or uses an untrusted URL, **When** remote import is attempted, **Then** IAM rejects the fetch safely.
4. **Given** a disabled application has a trusted manifest URL, **When** a non-destructive newer remote manifest is imported by an owner or maintainer, **Then** IAM accepts it while keeping the application disabled and non-assignable for new grants.

---

### User Story 5 - Review tombstones, replacement guidance, and diagnostics (Priority: P4)

An administrator reviews removed permission history, annotates replacement guidance, and diagnoses integrity problems.

**Why this priority**: Tombstone history and diagnostics are not required for day-one assignment, but they are necessary for auditability, remediation, and support workflows.

**Independent Test**: Remove a permission, view its tombstone history, add replacement guidance as an admin, and verify role diagnostics report malformed or orphaned assignments as integrity issues rather than normal permission states.

**Acceptance Scenarios**:

1. **Given** a removed permission tombstone exists, **When** an admin requests removed/history data explicitly, **Then** IAM returns tombstone metadata and audit history without exposing it in default catalog results.
2. **Given** an admin annotates a removed permission with replacement guidance, **When** the replacement permission exists, **Then** IAM stores and audits the guidance.
3. **Given** a role somehow contains malformed, unknown, or orphaned permission strings, **When** diagnostics run, **Then** IAM reports integrity issues for remediation; normal role views do not treat them as expected permission categories.

### Edge Cases

- Application identifiers collide with platform permission namespaces or OAuth client ids.
- `manifestBaseUrl` is already assigned to another current application.
- Manifests include duplicate local permission keys.
- Manifests use uppercase, malformed, too-short, too-long, wildcard, or deep-hierarchy permission keys.
- A newer manifest contains no permission changes but advances application version.
- A newer manifest re-declares a removed permission or deleted application identifier without acknowledgement.
- A role assignment preserves a disabled-application permission during replacement.
- A wildcard grant remains after all concrete permissions in its aggregate disappear because of out-of-band data drift.
- Remote manifest fetch attempts redirects, unsupported content type, oversized payloads, slow responses, wrong application id, or untrusted origins.
- Token issuance encounters unresolved dynamic permissions or wildcard expansion failure.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: IAM MUST expose application permission registry APIs under `/api/admin/application-permissions`.
- **FR-002**: IAM MUST use `/api/admin/application-permissions/applications` for registered resource/API applications.
- **FR-003**: IAM MUST use `applicationIdentifier + ":" + permissionKey` as the canonical `fullPermissionKey` stored in RBAC assignments and audit records.
- **FR-004**: IAM MUST treat permission IDs as internal/admin resource identifiers, not authorization values.
- **FR-005**: IAM MUST validate `application.id` as strict lowercase kebab case matching `^[a-z][a-z0-9-]{2,62}$`.
- **FR-006**: IAM MUST reject application identifiers colliding with platform permission namespaces or OAuth client ids.
- **FR-007**: IAM MUST validate local permission keys as exactly `resourceOrAggregate:action`, where each segment matches `^[a-z][a-z0-9-]{1,62}$`.
- **FR-008**: IAM MUST reject dynamic concrete permission keys with uppercase, wildcard segments, empty segments, or deeper hierarchies.
- **FR-009**: IAM MUST keep `applicationIdentifier` and `permissionKey` immutable after creation.
- **FR-010**: IAM MUST support complete manifests with `schemaVersion`, `application`, and `permissions`.
- **FR-011**: IAM MUST support only explicit manifest schema version `1.0.0` initially.
- **FR-012**: IAM MUST require `application.version` to be valid SemVer 2.0.0 without build metadata.
- **FR-013**: IAM MUST accept SemVer prerelease versions and compare them by SemVer precedence.
- **FR-014**: IAM MUST reject same or older manifest versions for existing current applications.
- **FR-015**: IAM MUST allow no-op newer manifest imports and audit version advancement.
- **FR-016**: IAM MUST keep owner, maintainers, trust settings, and enabled/disabled state locally managed in IAM, not manifest-managed.
- **FR-017**: IAM MUST support active and disabled current applications only; deleted applications are tombstones, not a current status.
- **FR-018**: IAM MUST exclude disabled applications from assignable catalog results by default and allow `includeDisabled=true` for review contexts.
- **FR-019**: IAM MUST treat permissions as currently defined or absent; permissions MUST NOT have lifecycle states.
- **FR-020**: IAM MUST soft-remove permissions as tombstones with `removedAt`, `removedBy`, and `removeReason`.
- **FR-021**: IAM MUST soft-delete applications as tombstones with `deletedAt`, `deletedBy`, and `deleteReason`.
- **FR-022**: IAM MUST expose tombstones only through explicit `includeRemoved`, `includeDeleted`, or history endpoints.
- **FR-023**: IAM MUST require `acknowledgeRedeclare=true` to re-register a deleted application identifier or re-declare a removed permission.
- **FR-024**: IAM MUST require a strictly newer manifest version when re-registering a deleted application identifier.
- **FR-025**: IAM MUST derive dynamic aggregate wildcard entries as `application:resourceOrAggregate:*` when at least one current concrete permission exists for that aggregate.
- **FR-026**: IAM MUST NOT allow dynamic `application:*` as an assignable dynamic permission.
- **FR-027**: IAM MUST return dynamic wildcard catalog entries from `/api/admin/application-permissions/catalog` marked as `kind: wildcard`.
- **FR-028**: IAM MUST keep `/api/admin/application-permissions/catalog` scoped to dynamic application permissions only.
- **FR-029**: IAM MUST expose platform permissions separately, adding `GET /api/admin/permissions/platform` if no clean endpoint exists.
- **FR-030**: IAM MUST require `roles:read` for the platform permission catalog.
- **FR-031**: IAM MUST require broad-grant acknowledgement for newly added platform wildcards, dynamic aggregate wildcards, and `*`.
- **FR-032**: IAM MUST preserve `permissions: string[]` in role response contracts.
- **FR-033**: IAM MUST add `acknowledgeWildcardGrant?: boolean` to role create, set-permissions, and add-permission request shapes.
- **FR-034**: IAM MUST reject unknown, invalid, tombstoned, deleted-application, or not-assignable permissions in new role assignments.
- **FR-035**: IAM MUST allow `SetRolePermissions` to preserve existing disabled-application assignments while rejecting newly added disabled-application assignments.
- **FR-036**: IAM MUST allow role permission removal for disabled-application or integrity-issue strings without registry validation.
- **FR-037**: IAM MUST expand wildcard grants to concrete permissions for token and introspection emission.
- **FR-038**: IAM MUST NOT emit wildcard strings in tokens or introspection responses.
- **FR-039**: IAM MUST validate concrete dynamic permissions against the registry before durable token issuance.
- **FR-040**: IAM MUST fail token issuance closed when dynamic permission validation or wildcard expansion fails.
- **FR-041**: IAM MUST allow introspection to return `active` while omitting unresolved dynamic permissions and logging/auditing enrichment failures.
- **FR-042**: IAM MUST omit unscoped dynamic application permissions from tokens.
- **FR-043**: IAM MUST keep platform `*` scoped to platform/admin permissions and not grant dynamic application resource permissions.
- **FR-044**: IAM MUST update `Permissions.Matches` to support multi-segment prefix wildcards in the role/wildcard slice.
- **FR-045**: IAM MUST require preview endpoints for AdminWeb destructive flows, while apply endpoints recalculate impact and do not depend on prior preview.
- **FR-046**: IAM MUST automatically remove exact assignments for removed permissions and deleted applications.
- **FR-047**: IAM MUST automatically remove derived wildcard assignments when the last current concrete permission in an aggregate is removed.
- **FR-048**: IAM MUST commit permission/application tombstones, assignment removals, manifest version changes, and audit records atomically.
- **FR-049**: IAM MUST use the initiating human/client actor for automatic assignment removal audit events.
- **FR-050**: IAM MUST require `application-permissions:admin` for destructive operations that remove permissions or applications and mutate assignments.
- **FR-051**: IAM MUST require only `application-permissions:admin`, not also `roles:assign`, for automatic assignment removals caused by registry admin operations.
- **FR-052**: IAM MUST require both `application-permissions:read` and `roles:read` to expose role names/details in impact previews; otherwise counts may be returned.
- **FR-053**: IAM MUST allow owners/maintainers to apply non-destructive manifest updates for owned/maintained active or disabled applications.
- **FR-054**: IAM MUST require admin rights for ownership transfer, enable/disable, application delete, permission delete, host/scheme/port trust-origin changes, and destructive manifest updates.
- **FR-055**: IAM MUST allow current owners and admins to add/remove delegated maintainers with a required reason.
- **FR-056**: IAM MUST treat group owners and group maintainers as granting authority to current group members at request time.
- **FR-057**: IAM MUST validate `manifestBaseUrl` as HTTPS in production with only localhost/dev exceptions, reject query/fragment, normalize trailing slashes, and require uniqueness among current applications.
- **FR-058**: IAM MUST derive remote import URL as `{manifestBaseUrl}/.well-known/permissions`.
- **FR-059**: IAM MUST reject remote import redirects, wrong application ids, unsupported content types, oversized responses, slow responses, and untrusted origins.
- **FR-060**: IAM MUST keep remote manifests public over HTTPS in `006`; authenticated remote fetch and signatures are out of scope.
- **FR-061**: IAM MUST support removed-permission replacement guidance as admin-only tombstone annotation.
- **FR-062**: IAM MUST require replacement guidance to point to a current existing platform or dynamic permission.
- **FR-063**: IAM MUST treat malformed, unknown, orphaned, or collapsed wildcard role strings as integrity issues rather than expected role permission states.
- **FR-064**: AdminWeb MUST use `Application Permissions` as the navigation label, not `Applications`.
- **FR-065**: AdminWeb MUST ship with backend workflows in each `006` vertical slice.

### Key Entities

- **Registered Permission Application**: A resource/API application namespace that exposes dynamic permissions. It has an immutable `applicationIdentifier`, display metadata, current manifest version, active/disabled state, owner, maintainers, optional trusted manifest base URL, and audit metadata.
- **Application Permission**: A current concrete permission under one registered application. It has an immutable local `permissionKey`, canonical `fullPermissionKey`, display metadata, optional category, creation/update metadata, and tombstone lineage when re-declared.
- **Derived Aggregate Wildcard**: A non-persisted assignable grant `application:resourceOrAggregate:*` derived from current concrete permissions under the aggregate.
- **Permission Manifest**: A complete, versioned JSON document published inline or at `/.well-known/permissions` containing resource/API application identity, display metadata, version, and current concrete permissions.
- **Permission Assignment**: A persisted RBAC assignment string stored initially on roles and later potentially on other assignment stores.
- **Permission Tombstone**: Historical record for a removed permission, with removal metadata and optional replacement guidance.
- **Application Tombstone**: Historical record for a deleted registered permission application.
- **Platform Permission**: Code-defined IAM/admin permission from `OpenIdentityStack.Application.Authorization.Permissions`.

## Security And Operational Impact *(mandatory)*

- **Authentication/Authorization**: Registry read is broad for authenticated readers with `application-permissions:read`; mutation is scoped by ownership/maintainer/admin rights. Destructive assignment-mutating operations require `application-permissions:admin`.
- **Assignment Safety**: Role mutations validate built-in and dynamic permissions through one classifier/assignability service. Broad grants require explicit acknowledgement and audit.
- **Token Safety**: Wildcard grants are never emitted. Durable token issuance fails closed on registry resolution errors. Introspection omits unresolved permissions while preserving token activity semantics.
- **Remote Fetch Safety**: Remote import only uses trusted registered base URLs, HTTPS in production, no redirects, strict content-type/size/timeout limits, and strict application id matching.
- **Audit Events**: Audit records must be in the same transaction as registry and assignment mutations. Records include actor, reason where required, before/after summaries, assignment removals, broad-grant acknowledgement, remote import failures, and correlation id.
- **Operations**: Clean database/destructive reset required for this alpha feature. No compatibility guarantee with arbitrary pre-existing role permission strings.

## Test Strategy *(mandatory)*

- **Unit Tests**: Permission key parsing, SemVer comparison, assignability classification, multi-segment wildcard matching, manifest validation, broad-grant detection, and tombstone/redeclare rules.
- **Application/Domain Tests**: Manifest apply rules, ownership/maintainer authorization, destructive omission planning, automatic assignment mutation through `IPermissionAssignmentStore`, and transaction failure behavior.
- **API/Integration Tests**: Security-sensitive boundaries for create/update/import/delete, owner/maintainer/admin/unrelated writer access, group principal resolution, role assignment validation, token issuance expansion, and introspection filtering/omission behavior.
- **Contract Tests**: Required for all project-owned `/api/admin/*` endpoints introduced or changed by this spec.
- **AdminWeb Tests**: Focused E2E per vertical slice plus targeted component/API-client tests for validation and preview flows.
- **Validation Commands**: Each slice must define focused `dotnet test`, AdminWeb `npm run build`, `npm run lint`, `npm test`, and focused E2E commands in tasks/quickstart before implementation begins.

## Documentation And Deployment Impact *(mandatory)*

- **Documentation**: Add numbered Spec Kit docs for `006`; retire or archive non-authoritative Copilot application-permission registry specs after `006` is accepted.
- **Deployment**: Pre-1.0 alpha breaking change. Clean database required; no preservation of pre-existing role permission data.
- **AdminWeb**: Adds `Application Permissions` navigation and workflows across five vertical slices.
- **API Compatibility**: Role `permissions: string[]` remains preserved. Existing application-permissions route names may change to target-specific manifest/import endpoints.

## Success Criteria *(mandatory)*

- **SC-001**: An authorized owner can register a resource/API permission application and at least five permissions from AdminWeb in under 10 minutes.
- **SC-002**: Same-version and older-version manifests are rejected 100% of the time.
- **SC-003**: New role assignments reject 100% of invalid, unknown, deleted, tombstoned, application-wide wildcard, or disabled-application permissions.
- **SC-004**: Broad grants require acknowledgement in 100% of create/add/set role mutation paths where the broad grant is newly added.
- **SC-005**: Tokens and introspection responses emit zero wildcard permission strings.
- **SC-006**: Destructive manifest/delete operations remove affected exact and collapsed wildcard role assignments transactionally and audit the detailed impact.
- **SC-007**: Remote import accepts only trusted, matching, bounded, valid manifests and rejects unsafe fetch behavior.
- **SC-008**: AdminWeb requires preview before destructive import/delete flows.

## Assumptions

- OAuth client applications and registered permission applications are related concepts but distinct resources.
- `application.id` in manifests identifies the resource/API permission namespace, not an OAuth client id.
- Runtime authorization may continue to honor existing role strings for disabled applications, but disabled application permissions are not assignable for new grants.
- Resource APIs that need fresh dynamic authorization use introspection; compact JWT dynamic claims may be stale until expiry.
- Exact OpenAPI DTO field details will be finalized in the contract artifact before implementation tasks are generated.
