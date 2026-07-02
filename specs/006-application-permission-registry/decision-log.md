# 006 Application Permission Registry - Decision Log

Status: captured from grill-me session for future spec creation.

## Spec Governance

- Only numbered Spec Kit specs under `specs/NNN-*` are authoritative.
- Numbered specs are cumulative in numeric order.
- Higher-numbered accepted specs may implicitly supersede or alter earlier specs.
- When a newer accepted spec contradicts an earlier one, implementation follows the newer spec and records the contradiction in implementation notes or the PR description.
- Status gates implementation readiness, not precedence. Draft specs are for planning/discussion only.
- Implemented portions of `001-openiddict-iam` are accepted baseline behavior even though the spec is marked Draft.
- Current implementation wins for stale baseline behavior unless a newer accepted spec explicitly carries the requirement forward.
- `004-native-aot-backend` is postponed and non-authoritative for current implementation because EF Core blocks Native AOT.
- `002-remove-banned-packages` is a permanent architecture rule: no MediatR, no Swashbuckle; use direct use-case/query-handler injection and Scalar/Microsoft OpenAPI.
- `003-test-coverage-improvement` is a permanent quality bar with pragmatic interpretation.
- `005-introspection-endpoint` is closed for current accepted specs.
- Application-permission registry work must be promoted into a clean new `006-application-permission-registry` spec before further implementation.

## Testing Rules

- Require at least one API/integration test for every externally visible security-sensitive behavior.
- Require contract tests for project-owned APIs under `/api/admin/*` and public module endpoints.
- Protocol endpoints mostly owned by OpenIddict, such as `/connect/token` and `/connect/introspect`, need focused API/integration tests and lightweight OpenAPI/spec artifacts when useful, but not full contract tests.
- `006` must include tests for role assignment validation, broad-grant acknowledgement, wildcard behavior, disabled-application preservation, destructive import/delete, and contract coverage for changed `/api/admin/*` routes.
- Add focused unit tests for multi-segment wildcard matching:
  - `orders-api:order:*` matches `orders-api:order:cancel`.
  - `orders-api:order:*` does not match `orders-api:invoice:cancel`.
  - `orders-api:*` matches at the low-level matcher.
  - assignment validation rejects dynamic application-wide wildcards.
  - existing platform wildcard behavior still works.

## Architecture And API Style

- Prefer the current dominant implementation style for the area being touched.
- Admin CRUD/resource APIs should use Minimal API endpoint mapper classes.
- OIDC/account browser flows may keep controller/Razor patterns unless a later accepted spec changes that.
- Application-layer ports should be used for cross-aggregate persistence/assignment operations.
- Use an Application-layer `IPermissionAssignmentStore` abstraction, initially implemented for roles.
- Use cases define atomic operations; Infrastructure implements transaction boundaries.
- Expose a unit-of-work/transaction abstraction to Application if needed.
- Audit writes must always be inside the same transaction as registry and assignment mutations.

## Permission Identity Model

- `applicationIdentifier` is the namespace.
- `permissionKey` is the local stable key.
- `fullPermissionKey = applicationIdentifier + ":" + permissionKey` is the canonical RBAC/audit string.
- Permission `Guid` is an internal/admin API resource identifier, not the authorization value.
- Application identifiers are immutable.
- Permission keys are immutable after creation.
- Re-declaring a removed permission creates a new current record with historical linkage; it does not reuse the old `Guid`.
- Re-registering a deleted application creates a new current application record linked to prior deleted lineage; it does not restore prior role assignments.

## Permission Key Shape

- Dynamic concrete permission full key hierarchy is `application:resourceOrAggregate:action`.
- Manifest permission entries use local key `resourceOrAggregate:action`; the application id is supplied once in the manifest application block.
- Local permission key must have exactly two segments.
- `applicationIdentifier` format: strict normalized kebab case, `^[a-z][a-z0-9-]{2,62}$`.
- `resourceOrAggregate` and `action` segments: `^[a-z][a-z0-9-]{1,62}$`.
- Reject uppercase rather than silently normalizing external manifests.
- Broad action names like `manage` or `admin` are allowed.
- Application identifiers must not collide with platform/built-in permission namespaces.

## Platform And Dynamic Permissions

- Built-in platform permissions remain code-defined and separate from dynamic application permissions.
- `GET /api/admin/application-permissions/catalog` returns only dynamic application permissions.
- Add `GET /api/admin/permissions/platform` if no clean platform catalog endpoint exists.
- `GET /api/admin/permissions/platform` requires `roles:read`.
- Platform catalog includes concrete, wildcard, and super-admin permissions marked as `kind: concrete|wildcard|superAdmin`.
- Add a separate method such as `GetAssignablePlatformPermissions()` rather than changing `GetAllPermissions()` if policy registration expects concrete permissions.
- Authorization policy registration should continue using concrete built-in permissions only.

## Wildcard Semantics

- Dynamic `application:*` grants are not assignable.
- Dynamic aggregate wildcards `application:resourceOrAggregate:*` are allowed.
- Dynamic aggregate wildcards are derived automatically from concrete registered permissions, not persisted as permission rows.
- A derived wildcard exists only when at least one current concrete permission exists for that aggregate.
- If the last concrete permission for an aggregate is removed, the derived wildcard disappears from the assignable catalog.
- Existing wildcard grants automatically cover newly registered future concrete permissions for that aggregate.
- Adding a concrete permission under an aggregate with existing wildcard assignments requires wildcard-impact acknowledgement.
- Wildcard impact is admin-visible but not a hard block if `acknowledgeWildcardImpact=true`.
- Removing a concrete permission reports impacted wildcard assignments.
- If the removed concrete permission is the last remaining permission for that aggregate, wildcard role assignments for that aggregate are automatically unassigned.
- Catalog wildcard entries use `fullPermissionKey`, have no permission ID, and are marked `kind: wildcard`.
- Catalog wildcard entries are returned in the same catalog endpoint as concrete permissions.
- Catalog wildcard entries are sorted before concrete permissions within each aggregate.
- Catalog search should match wildcard key, aggregate, generated display label, and covered concrete permission display names/keys.
- Wildcard catalog entries include count plus a small summary, not a full covered permission list by default.
- Wildcard display name is generated deterministically, e.g. `All {aggregate display name} permissions`.
- No explicit aggregate display metadata in `006`; use simple title-cased aggregate fallback and optional category inference.
- Low-level `Permissions.Matches` should become a generic multi-segment prefix wildcard matcher in slice 2.
- Assignment validation, not the matcher, enforces forbidden dynamic `application:*` and `*` registry assignment rules.
- `Permissions.Matches("*", anything)` remains valid for platform super-admin.
- Role storage keeps wildcard strings, but token and introspection permission emission expands wildcard grants to current concrete permissions.
- Tokens and introspection must never emit wildcard strings such as `*`, `users:*`, or `orders-api:order:*`.
- Dynamic aggregate wildcards expand to current concrete permissions for that aggregate, including disabled applications for existing assignments.
- Dynamic wildcard expansion excludes tombstoned/removed permissions.
- If an aggregate wildcard assignment has no current concrete permissions, expansion emits nothing and diagnostics flag an integrity issue.
- Platform wildcards and `*` expand to concrete platform permissions for platform/admin permission emission.
- Platform `*` does not grant dynamic application resource permissions.
- Dynamic application permissions are emitted only when scoped to the target resource/audience. If no explicit audience/resource is requested, omit dynamic application permissions.
- `006` states the no-unscoped-dynamic-permission rule without redesigning OAuth resource indicators.
- Introspection remains the preferred mechanism for resource APIs needing fresh dynamic permissions.
- Token issuance resolves and validates dynamic concrete and wildcard permissions against the registry.
- Token issuance fails closed if dynamic permission validation or wildcard expansion fails.
- Introspection returns `active` without unresolved/invalid dynamic permissions and emits no wildcard; it logs/audits enrichment/integrity failures.

## Broad Grant Acknowledgement

- Broad grants include platform wildcards, dynamic aggregate wildcards, and `*`.
- Broad-grant assignment requires `acknowledgeWildcardGrant=true`.
- This applies to `CreateRole`, `AddRolePermission`, and newly added broad grants in `SetRolePermissions`.
- Existing broad grants preserved during `SetRolePermissions` do not require re-acknowledgement.
- Duplicate checks happen before acknowledgement checks.
- `roles:write` plus acknowledgement is enough to create a role with `*`.
- `roles:assign` plus acknowledgement is enough to add/set `*` on an existing role.
- `409` broad-grant conflict responses include stable machine-readable codes and structured items.
- Suggested top-level code: `RolePermissions.BroadGrantAcknowledgementRequired`.
- Items include `{ permission, kind, code, warning }`.
- Broad-grant acknowledgement is audited only when broad grants are newly added.
- Management Web should visually distinguish `*` more strongly, with text that it grants every current and future permission in the system.

## Role Assignment Validation

- Preserve `permissions: string[]` on role responses.
- Add enriched role permission state through a dedicated endpoint first, e.g. `GET /api/admin/roles/{id}/permission-details`.
- Optional inline enrichment can come later via `includePermissionDetails=true`.
- Role mutation request shapes remain string-based:
  - `CreateRoleRequest` adds `acknowledgeWildcardGrant?: boolean`.
  - `SetRolePermissionsRequest` adds `acknowledgeWildcardGrant?: boolean`.
  - `AddPermissionRequest` adds `acknowledgeWildcardGrant?: boolean`.
- Built-ins from platform permissions are valid.
- Dynamic concrete permissions must exist in the registry.
- Dynamic aggregate wildcards must be derived from current concrete permissions.
- `CreateRole` rejects disabled-application permissions, invalid/missing permissions, and unacknowledged wildcard grants.
- `AddRolePermission` rejects disabled-application permissions, invalid/missing permissions, and unacknowledged wildcard grants.
- `SetRolePermissions` validates newly added assignments against assignability.
- `SetRolePermissions` may preserve existing disabled-application permissions.
- `SetRolePermissions` rejects integrity-issue permissions even if unchanged.
- `RemoveRolePermission` may remove disabled-application and integrity-issue permissions without registry validation.
- Assignment validation should use the same assignability service as the catalog.
- The assignability/classifier service classifies platform and dynamic permissions together.
- Classifier reason codes include `InvalidFormat`, `UnknownPermission`, `DisabledApplication`, `WildcardAcknowledgementRequired`, `ApplicationWideWildcardNotAllowed`, `TombstonedPermission`, `DeletedApplication`.
- Use distinct reason codes for tombstoned permissions/deleted applications when history is known.
- Safe tombstone details can be exposed to authorized admin callers.

## Integrity Model

- Permissions should not normally be missing, invalid, or unavailable in role data.
- Missing/invalid/orphaned role permissions are data integrity issues, not normal UI states.
- Normal role permission details should not normalize integrity issues as ordinary categories.
- Diagnostics may expose integrity problems for remediation.
- Disabled-application assignments can remain and are not integrity issues.
- Suggested role permission categories: `valid`, `disabledApplication`, and `integrityIssue`.
- Because the project is pre-1.0 alpha, breaking changes are allowed.
- `006` may assume a clean database.
- Applying `006` requires a clean database/destructive reset; no migration compatibility guarantee.
- Tests assuming arbitrary permission strings can be assigned should be replaced.
- Seed order: built-in platform permissions, then application registry/manifests, then roles referencing dynamic permissions.

## Application State And Deletion

- Current application state set is only `active` and `disabled`.
- `retired` is removed/superseded.
- Application deletion is represented by soft-delete/tombstone metadata, not status.
- Current application endpoints expose only active/disabled applications by default.
- Deleted applications appear only with `includeDeleted=true` or history endpoints.
- Application tombstone fields: `deletedAt`, `deletedBy`, `deleteReason`.
- Application delete requires `application-permissions:admin`, concurrency, reason, audit.
- Application delete auto-tombstones current permissions and removes exact concrete and derived wildcard role assignments belonging to the application.
- Application delete is transactional and reports all assignment changes.
- Application delete preview endpoint: `GET /api/admin/application-permissions/applications/{id}/deletion-impact`.
- Direct `DELETE /applications/{id}` is allowed; Management Web should preview first.
- Re-registering a deleted application with the same identifier requires `acknowledgeRedeclare=true`, admin rights, and a strictly newer manifest version than the last accepted version for that identifier.
- Re-registration does not restore previously removed role assignments.
- Disabled applications appear in list/detail endpoints by default.
- Disabled applications are excluded from assignable catalog by default, but `includeDisabled=true` allows review/admin contexts.
- Disabling an application does not unassign permissions.
- Disabling stops permissions from being offered for new assignment.
- Runtime authorization continues to evaluate existing role permission strings for disabled apps.
- Disabling/enabling requires `application-permissions:admin`, reason, concurrency, and audit.
- Disabling should have an impact preview showing affected roles, even though assignments are not removed.

## Permission Removal And Tombstones

- Permissions have no lifecycle. They are either currently defined or absent.
- Removed permissions are soft-deleted/tombstoned.
- Permission tombstone fields: `removedAt`, `removedBy`, `removeReason`.
- Tombstoned permissions are excluded from default application detail/catalog endpoints.
- Tombstoned permissions are exposed only with explicit `includeRemoved=true` or history endpoints.
- Tombstones are read-only except for narrow admin annotations such as replacement guidance.
- `DELETE /api/admin/application-permissions/permissions/{permissionId}` removes a permission from the current registry.
- Permission delete requires concurrency and reason.
- Stale concurrency returns `412 Precondition Failed`.
- Existing exact dependencies do not cause `409`; deletion removes exact assignments transactionally.
- `409` is reserved for policy/state conflicts not solved by unassignment.
- Normal callers get `404` for already removed permissions.
- Permission deletion preview endpoint: `GET /api/admin/application-permissions/permissions/{permissionId}/deletion-impact`.
- `GET /permissions/{permissionId}/dependencies` remains a general usage endpoint and includes `exactAssignments` and `wildcardAssignments`.
- Dependencies for a concrete permission include wildcard assignments that currently cover it.
- Dependency reads cover persisted assignment sources only, not issued tokens/sessions.
- Historical unassignments are in deletion/import results and audit history, not current dependencies.
- Permission removal result DTOs separate `removedPermissions`, `exactAssignmentsRemoved`, `wildcardAssignmentsRemoved`, `wildcardAssignmentsImpacted`, `metadataUpdated`, and `manifestVersionAdvanced`.
- Result DTOs include role IDs and display names.

## Automatic Assignment Mutation

- Manifest omission and manual permission/application delete automatically remove affected assignments.
- Exact role assignments for removed concrete permissions are automatically removed.
- Wildcard assignments remain when other concrete permissions still exist for the aggregate.
- Wildcard assignments are automatically removed when the last concrete permission for the aggregate is removed.
- Permission tombstone, assignment removals, wildcard collapses, manifest version update, and audit records commit atomically.
- If any assignment removal fails, the whole operation rolls back and the previous manifest/current registry state remains.
- Automatic unassignment uses the human/client actor who initiated the operation.
- `application-permissions:admin` is sufficient for automatic role unassignment; `roles:assign` is not also required.
- Role audit history should show assignment removals caused by registry actions.
- Automatic unassignment events record the cause, such as `ApplicationPermissionRemoved` or `ApplicationManifestImported`.
- No automatic token/session revocation in `006`.
- Compact JWTs may contain removed permission claims until expiry; APIs needing fresh authorization use introspection.

## Manifests

- `006` defines the `/.well-known/permissions` manifest contract.
- Manifest endpoint is public over HTTPS.
- Manifest contract:
  - `schemaVersion`
  - `application { id, displayName, description?, version }`
  - `permissions[] { key, displayName, description?, category? }`
- Manifests are complete, not partial.
- A newer manifest that omits a permission removes/tombstones it.
- A manifest can update application display metadata and permission metadata.
- Ownership, maintainers, trust settings, and application enabled/disabled state are IAM-managed and not set by the manifest.
- Manifest `schemaVersion` follows SemVer and initially supports only explicit `"1.0.0"`.
- Unsupported schema versions are rejected.
- `application.version` follows SemVer 2.0.0 without build metadata.
- Prerelease versions are accepted and compared by SemVer precedence.
- Build metadata is rejected.
- Manifest version is per application globally, regardless of inline or remote source.
- Existing applications require strictly newer manifest versions for inline and remote imports.
- Same-version imports return `409 Conflict`, even if content is identical.
- Initial registration allows valid SemVer including `0.x`.
- Accept version bumps with no permission/metadata changes.
- No-op version bumps are audited as low-detail successful imports.
- Import preview for no-op version bump returns `hasChanges: false` and `versionWillAdvance: true`.
- Application list/detail includes `manifestVersion` and likely `lastManifestImportedAt`.
- No filtering by manifest freshness in `006`.
- `description` is optional but strongly recommended.
- `displayName` is required.
- Permission `category` is optional and defaults to `resourceOrAggregate` for grouping.
- Category is UI metadata and mutable; it has no authorization semantics.
- Category may group permissions from multiple aggregates, but wildcard entries remain per aggregate.
- Manifest display/category changes do not need special acknowledgement.

## Manifest Endpoints

- `POST /api/admin/application-permissions/applications` creates from inline manifest plus IAM-managed wrapper fields.
- Initial create request wrapper includes `manifest`, `ownerId`, `ownerType`, optional `manifestBaseUrl`, and maybe `acknowledgeRedeclare`.
- Initial create supports `acknowledgeRedeclare` for deleted application re-registration.
- Initial create does not support `acknowledgeWildcardImpact`.
- Existing application updates are target-specific.
- `POST /api/admin/application-permissions/applications/{id}/manifest/preview` previews inline manifest update.
- `POST /api/admin/application-permissions/applications/{id}/manifest` applies inline manifest update.
- `POST /api/admin/application-permissions/applications/{id}/import/preview` previews remote import from registered base URL.
- `POST /api/admin/application-permissions/applications/{id}/import` applies remote import from registered base URL.
- Existing top-level `POST /applications/import` should be replaced by target-specific import.
- Manifest update endpoints do not change owner, maintainers, trust origin, or enabled/disabled state.
- Manifest update wrappers support `acknowledgeRedeclare` and `acknowledgeWildcardImpact`.
- `acknowledgeWildcardImpact` applies only when adding concrete permissions to an already-current application and existing wildcard assignments for that aggregate would expand.
- Disabled applications may accept manifest updates.
- Adding concrete permissions to disabled applications is allowed for owners/maintainers with write rights plus wildcard-impact acknowledgement if needed.
- Destructive manifest updates that remove permissions and unassign roles require `application-permissions:admin`.
- If an owner without admin submits a destructive manifest, fail the entire import with `403 Forbidden`.
- Do not partially apply non-destructive changes from a destructive manifest when the caller lacks admin rights.

## Manifest Import Preview

- Preview endpoints are side-effect free.
- Preview validates SemVer ordering, additions, updates, omissions, tombstone redeclarations, exact unassignments, wildcard impacts/collapses, and required permissions.
- Preview should report whether caller has sufficient rights.
- Preview for non-admin owners may show destructive impact if caller has read access to application and role dependency data.
- Role names/details require both `application-permissions:read` and `roles:read`; otherwise return counts and indicate details are omitted.
- Preview endpoints require `application-permissions:read`.
- Apply endpoints require write/admin depending on destructiveness.
- Management Web must require preview in its own flow before enabling destructive import/delete.
- The API does not require prior preview; final apply calls recalculate impact.

## Remote Manifest Trust And Fetching

- Remote import is allowed only from trusted/registered application base URLs.
- First remote import cannot bootstrap trust from an arbitrary URL.
- `manifestBaseUrl` is set during inline registration or application metadata update.
- Remote import targets an existing application resource, e.g. `POST /applications/{id}/import`.
- Fetched manifest `application.id` must strictly match the registered application identifier.
- Do not let fetched content choose the target namespace.
- Remote fetch derives manifest URL as `{manifestBaseUrl}/.well-known/permissions`.
- Store origin plus optional base path; normalize trailing slashes; reject query/fragment.
- `manifestBaseUrl` is mutable with elevated audit.
- Owners may update same-origin path-only changes.
- Host/scheme/port changes require admin rights.
- Production requires HTTPS.
- Development may allow `http://localhost`, `http://127.0.0.1`, and maybe Aspire service discovery URLs only in development.
- Do not allow arbitrary internal HTTP hosts.
- Remote import should not follow redirects.
- Remote import enforces content type (`application/json` or `application/permission-manifest+json`), response size limits, short timeout, and defensive rejection of oversized/wrong/slow responses.
- No manifest checksum/signature in `006`; rely on HTTPS plus trusted base URL.
- Authenticated/private remote manifest fetch is out of scope for `006`.

## Ownership And Authorization

- Owner supports both user and group/team principals via `{ ownerId, ownerType }`.
- Delegated maintainers support both user and group principals via `{ principalId, principalType }`.
- Group ownership/maintainer authorization resolves current group membership at request time.
- `application-permissions:read` allows reading all applications.
- `application-permissions:write` grants access to the feature but mutations are scoped to owned/maintained applications.
- `application-permissions:admin` bypasses ownership for administrative overrides.
- Users with `application-permissions:write` can create applications they own.
- `application-permissions:admin` is required to assign ownership to someone else, set privileged/protected remote trust origins, or re-register a deleted application.
- Delegated maintainers can update manifests and permission display metadata for their application.
- Delegated maintainers cannot change owner, maintainers, trust origin host/scheme/port, enable/disable state, or delete the application.
- Destructive manifest updates that remove permissions and unassign roles require admin rights.
- Owners/maintainers can add permissions under aggregates with existing wildcard grants if they acknowledge wildcard impact.
- No organization-level stricter policy hook in `006`.
- Application detail includes owner and delegated maintainers.
- Application detail includes `manifestBaseUrl` for callers who can manage/read application permissions.

## Management Web Requirements

- Include minimal Management Web application-permission management workflows in `006`.
- `006` must ship backend and Management Web workflows together.
- Build `006` as vertical slices rather than all backend first and UI later.
- First vertical slice is application registration and manifest management.
- Slice 1 includes inline manifest create, list, detail, and target-specific inline manifest update.
- Slice 1 explicitly rejects destructive manifest omissions with `409 DestructiveManifestChangeNotSupportedYet`.
- Slice 1 waits for the role/wildcard slice before handling wildcard-impact flows.
- Slice 1 includes optional `manifestBaseUrl` metadata on create/detail/update, but not remote fetch/import.
- Slice 1 enforces `manifestBaseUrl` validation rules immediately.
- Slice 1 implements ownership and delegated maintainer authorization fully.
- Slice 1 includes user and group owner/maintainer principals.
- Slice 1 includes minimal maintainer management: list maintainers on detail, add maintainer, remove maintainer.
- Slice 1 includes ownership transfer.
- Ownership transfer requires `application-permissions:admin`.
- Current owners and admins can manage delegated maintainers.
- Maintainers cannot add/remove maintainers unless admin.
- Any current member of a group owner can act as owner.
- Any current member of a delegated maintainer group can act as maintainer.
- Maintainer add/remove requires a reason.
- Manifest metadata updates do not require a reason.
- `manifestBaseUrl` changes require a reason.
- Enable/disable application requires a reason.
- Slice 1 includes active/disabled application state as read/display and admin action.
- Slice 1 should not stub disable impact as zero. If real role impact is unavailable, omit impact details and use a general warning.
- Slice 1 includes `GET /applications/{id}/disable-impact` only if it can return real role impact.
- Slice 1 does not include application delete/tombstone.
- Slice 1 uses only current defined permissions, not tombstones/redeclare.
- Slice 1 enforces strictly newer SemVer.
- Slice 1 uses current manifest version plus audit events; no manifest import history table yet.
- Slice 1 includes contract tests for all new/changed application endpoints.
- Slice 1 includes focused Management Web E2E for create from inline manifest, view detail, update with newer non-destructive manifest, and same/older version validation.
- Slice 1 includes API integration tests for owner, maintainer, admin, and unrelated writer authorization boundaries.
- Slice 1 includes group-owner/group-maintainer authorization integration tests.
- Slice 1 Management Web can initially accept raw principal IDs/types for owner/maintainer selection.
- Slice 1 Management Web should provide both structured manifest editor and raw JSON editor.
- For create, the structured editor supports adding/removing permissions.
- For update, the structured editor allows adding/editing metadata and blocks/reminds that removing existing permissions is not available until the destructive-change slice.
- Raw JSON update omissions may still be submitted; API returns `409 DestructiveManifestChangeNotSupportedYet` and UI shows it.
- No visible explanatory feature text about permission lifecycle/removal internals.
- Management Web nav label is `Application Permissions`.
- Do not use `Applications` as the nav label because OAuth 2.0 client applications already use that concept.
- In OAuth terms, application permissions target resource APIs/resources.
- Keep the API path `/api/admin/application-permissions/applications`.
- Keep manifest field `application.id` in `006`; clarify that it is the resource/API application identifier, not OAuth client id.
- Reject collisions between `application.id` and OAuth client identifiers.
- `manifestBaseUrl` must not be assigned to a different current registered permission application.
- Duplicate `application.displayName` values are allowed.
- Duplicate permission display names within an application are allowed.
- Duplicate local permission keys in the same manifest reject the entire request.
- Duplicate full permission keys across applications are prevented by unique application identifiers plus per-application local key uniqueness.
- Current application identifiers must be unique; deleted tombstone identifiers remain reserved unless intentionally re-declared with acknowledgement and strictly newer manifest version.
- `manifestBaseUrl` uniqueness applies only to current applications.
- Remote import for disabled applications is allowed.
- Disabled application imports that remove permissions require admin rights.
- Disabled application imports that only add/update metadata are allowed for owners/maintainers.
- Slice 1 does not include remote import preview/apply.
- Platform permissions endpoint and UI wait for the role picker slice.
- `Permissions.Matches` multi-segment wildcard change waits for the role/wildcard slice.
- Slice 2 is role picker and assignment validation.
- Slice 2 includes `GET /api/admin/permissions/platform` and Management Web unified role picker.
- Slice 2 includes strict role assignment validation for dynamic concrete permissions and platform permissions.
- Slice 2 includes derived aggregate wildcard catalog entries and assignment acknowledgement.
- Slice 2 changes `Permissions.Matches` to support multi-segment wildcard matching.
- Slice 2 includes token/introspection concrete-only expansion and dynamic permission validation, but not a full audience/resource redesign.
- Slice 2 includes introspection tests proving assigned aggregate wildcards are expanded to concrete permissions and relevant to the requesting application client.
- Introspection must keep platform permissions and `*` out of application-client filtered responses.
- Slice 2 Management Web E2E covers wildcard shown above aggregate, acknowledgement required, assignment succeeds, and role detail shows stored wildcard grant.
- Slice 2 Management Web E2E includes one platform broad-grant acknowledgement flow.
- Runtime token/introspection emission tests stay API-only.
- Disabled-application preservation behavior is API/integration tested, not E2E.
- Slice 3 is destructive manifest/delete workflows.
- Slice 3 includes destructive manifest omissions, manual permission delete, and application delete.
- Slice 3 Management Web must preview before destructive manifest apply, permission delete, and application delete.
- Slice 3 E2E uses one representative destructive flow, preferably destructive manifest omission; API/integration tests cover all destructive flows.
- Slice 3 shows operation results after destructive actions but does not add full tombstone history UI.
- Slice 4 is remote import and trust flow.
- Slice 4 includes target-specific remote import preview/apply and remote fetch security constraints.
- Slice 4 includes Management Web remote import UI.
- Slice 4 E2E covers successful remote import from a controlled local fixture endpoint.
- Slice 5 is tombstone history, replacement guidance, and diagnostics.
- Slice 5 is the final planned slice for `006`.
- The `006` implementation plan should explicitly list the five slices:
  1. Application registration and inline manifest management.
  2. Role picker, strict assignment validation, broad grants, catalog, and concrete-only token/introspection emission.
  3. Destructive manifest/delete workflows with previews, tombstones, auto-unassignment, and audit.
  4. Remote import and trust flow.
  5. Tombstone history, replacement guidance, and diagnostics.
- Management Web should require preview before destructive import/delete actions.
- Management Web should show destructive impact and automatic unassignment before confirmation.
- Destructive actions should be hidden or disabled with explanation for non-admin owners/maintainers when discoverability matters.
- Wildcard grants appear above each aggregate, visually distinct.
- Use human wording such as “All current and future order permissions,” not just “Wildcard.”
- Selecting a wildcard grant uses inline warning plus explicit acknowledgement checkbox, not a second modal.
- `*` should have stronger visual treatment and acknowledgement text explaining it grants every current and future permission in the system.
- Disabled-application assignments may be shown as from a disabled application.
- Integrity issues should be diagnostics/remediation, not normalized as ordinary categories.

