# Implementation Notes: Application Permission Registry

## Phase 1 Review

The repository already contains an application-permission registry implementation from earlier planning work. `006` keeps the useful application-permission naming direction, but tightens contracts and replaces several service-era or lifecycle concepts.

## Keep

- `src/OpenIdentityStack.Domain/ApplicationPermissions/` as the domain home for registered applications, permissions, owners, maintainers, and assignment dependency value objects.
- `src/OpenIdentityStack.Application/ApplicationPermissions/` as the use-case/query/DTO home.
- `src/OpenIdentityStack.Infrastructure/Persistence/ApplicationPermissions/` for EF configurations and repository implementation.
- `src/OpenIdentityStack.Api/Admin/ApplicationPermissionsApi.cs` as the Minimal API mapper style for registry endpoints.
- `src/OpenIdentityStack.AdminWeb/src/features/application-permissions/` as the AdminWeb feature area.
- Existing tests under `tests/**/ApplicationPermissions/` as regression coverage to evolve under the 006 contract.

## Rename Or Reshape

- Manifest DTOs must move from the previous `{ application: { id, name, version }, permissions[].name }` shape to the 006 complete manifest shape `{ schemaVersion, application: { id, displayName, description?, version }, permissions[].key }`.
- Existing top-level `POST /api/admin/application-permissions/applications/import` must be replaced by target-specific remote import routes under `/applications/{applicationId}/import`.
- Existing `/applications/{id}/lifecycle` must be replaced by explicit `/enable` and `/disable` routes. The `retired` lifecycle state is non-authoritative for 006.
- Existing ad hoc add-permission endpoints remain useful for internal maintenance patterns, but slice 1 user flows should use complete manifest preview/apply routes.
- Existing catalog output must distinguish concrete dynamic permissions from derived aggregate wildcards before role-picker work starts.

## Replace Or Remove

- Remove or supersede any `ServicePermission` route names, DTO labels, and contract text that imply the old service-permission registry.
- Remove the current permissive dynamic permission key behavior that allows one-segment local keys. 006 requires exactly `resourceOrAggregate:action`.
- Remove retired-permission and retired-application semantics from current registry behavior. 006 uses current records plus tombstones/history.
- Do not preserve arbitrary pre-006 role permission strings as valid normal data; diagnostics can report malformed or orphaned strings later.

## Pre-Implementation Constraints

- Contract tests for `/api/admin/*` endpoints must follow the OpenAPI artifact in `contracts/application-permission-registry.openapi.yaml`.
- New production code must be preceded by RED tests per the mandatory Superpowers TDD hook.
- This feature is an alpha breaking change and may require a clean database/destructive reset.
