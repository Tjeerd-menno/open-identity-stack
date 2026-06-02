# Research: Management Web AdminWeb Parity

## 1. Separate frontend app

- **Decision**: Keep ManagementWeb as a peer application beside AdminWeb.
- **Rationale**: Parallel rollout, independent deployability, and a Mantine-based UI direction can proceed without destabilizing AdminWeb.
- **Alternatives considered**: Extend AdminWeb in place; share one deployment artifact; gate the new UI behind a path split.

## 2. Mantine as the UI foundation

- **Decision**: Use Mantine as the visual and component foundation for ManagementWeb.
- **Rationale**: The user wants AdminWeb behavior ported into the new frontend with Mantine design, not a workflow redesign.
- **Alternatives considered**: Keep the existing AdminWeb shadcn-style UI; mix component systems; custom-build common controls.

## 3. Behavior parity before redesign

- **Decision**: Port behavior one-for-one first and redesign only the visual/component layer.
- **Rationale**: AdminWeb already encodes the expected operator workflows, validations, routes, and edge cases. Matching it first reduces product ambiguity and makes verification concrete.
- **Alternatives considered**: Redesign workflows while porting; launch only a subset of improved flows; preserve only backend endpoint coverage.

## 4. Shared foundation first

- **Decision**: Build ManagementWeb shared foundation to parity before new vertical slices.
- **Rationale**: API errors, token injection, 401 logout handling, permission gates, tables, dialogs, forms, and secret display would otherwise drift across slices.
- **Alternatives considered**: Implement Applications directly with local helpers; let each slice define its own patterns.

## 5. Permission normalization

- **Decision**: Normalize permission checks in the ManagementWeb foundation while keeping backend authorization authoritative.
- **Rationale**: The current partial Users slice uses permission names that do not line up cleanly with AdminWeb/backend constants. A shared matrix prevents inconsistent action visibility. ManagementWeb must read concrete grants from `permission`, `permissions`, `scope`, and `scp` claims in both OIDC profile data and access-token payloads because backend authorization-code issuance expands effective role permissions into concrete `permission` claims. Role names such as `admin` or `super-admin` are not frontend authorization grants.
- **Alternatives considered**: Preserve current partial Users permission names; duplicate AdminWeb's ad hoc checks exactly; rely only on backend 403 responses; treat an admin role claim as a frontend wildcard. The role-name wildcard approach was rejected because permissions should remain granular and backend authorization is the final authority.

## 6. Consolidated Applications only

- **Decision**: ManagementWeb uses only `/api/admin/applications` for application-like resources.
- **Rationale**: The unified Applications model replaces Clients and Service Accounts. ManagementWeb is the forward-looking UI and should not expose legacy surfaces.
- **Alternatives considered**: Keep Clients and Service Accounts pages for compatibility; add Applications plus legacy links; call legacy endpoints behind a unified UI.

## 7. Vertical slice order

- **Decision**: Deliver shared foundation first, then Applications, Users refactor, Roles, Groups, Sessions, Providers, Settings, Application Permissions, Audit, and Overview.
- **Rationale**: Applications is the highest-value strategic surface after foundation. Users already exists but must be refactored rather than treated as complete. Roles should precede slices that reuse permission catalog behavior.
- **Alternatives considered**: Finish Users first; port AdminWeb file order; implement Audit first.

## 8. Audit endpoint scope

- **Decision**: Add a ManagementWeb-only Audit section backed by one read-only `GET /api/admin/audit-entries` endpoint.
- **Rationale**: Audit entries already exist in persistence, but there is no admin query endpoint or AdminWeb screen. A single paged list endpoint is sufficient for v1.
- **Alternatives considered**: Frontend placeholder only; add a detail endpoint immediately; make Audit an AdminWeb parity requirement.

## 9. Audit response shape

- **Decision**: Include `details`, `beforeState`, and `afterState` in v1 list responses.
- **Rationale**: ManagementWeb can render expandable row details without a second endpoint. Payload risk is acceptable for the first paged version.
- **Alternatives considered**: List summary only with a detail endpoint; omit before/after state until later.
