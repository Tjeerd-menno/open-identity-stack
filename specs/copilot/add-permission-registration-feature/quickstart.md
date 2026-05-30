# Quickstart: Service/API Permission Registry Planning Artifacts

This quickstart is for future implementation tasks. It intentionally does not implement application code.

## Preconditions

- Repository root: `/home/runner/work/open-identity-stack/open-identity-stack`
- Feature branch: `copilot/add-permission-registration-feature`
- Runtime stack: .NET 10, ASP.NET Core Minimal APIs, OpenIddict, EF Core/Npgsql PostgreSQL, .NET Aspire
- Planning artifacts:
  - `specs/copilot/add-permission-registration-feature/spec.md`
  - `specs/copilot/add-permission-registration-feature/plan.md`
  - `specs/copilot/add-permission-registration-feature/research.md`
  - `specs/copilot/add-permission-registration-feature/data-model.md`
  - `specs/copilot/add-permission-registration-feature/contracts/service-permission-registry.openapi.yaml`

## Phase 2 Implementation Approach

Follow strict TDD in this order for each vertical slice:

1. Write failing domain tests for aggregate validation, stable key immutability, duplicate detection, reserved namespace protection, and lifecycle rules.
2. Add the minimal domain model required to pass.
3. Write failing application tests for use-case orchestration, authorization, validation, dependency handling, atomicity, concurrency, and audit outcomes.
4. Add application interfaces, commands, queries, validators, and use-case implementations to pass.
5. Write failing infrastructure tests for EF Core mappings, unique indexes, concurrency tokens, PostgreSQL persistence, and role-permission dependency reads.
6. Add persistence configuration, repositories, dependency readers, DI registration, and migrations to pass.
7. Write failing API and contract tests against the OpenAPI contract.
8. Add Minimal API endpoints, request/response DTOs, authorization policies, and OpenAPI metadata to pass.
9. Add regression/performance checks for service listing, permission catalog search, and dependency lookup.
10. Refactor while preserving all passing tests and constitution gates.

## Expected Future Test Commands

From `/home/runner/work/open-identity-stack/open-identity-stack`:

```bash
dotnet test tests/OpenIdentityStack.Domain.Tests
dotnet test tests/OpenIdentityStack.Application.Tests
dotnet test tests/OpenIdentityStack.Infrastructure.Tests
dotnet test tests/OpenIdentityStack.Api.Tests
dotnet test tests/OpenIdentityStack.Contract.Tests
dotnet test
```

## Expected Future Aspire Verification

After implementation tasks exist, use Aspire for end-to-end development verification:

```bash
cd /home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.AppHost
dotnet run
```

Verify:

- PostgreSQL resource is healthy.
- OpenIdentityStack API is healthy.
- OpenIddict login/token flows continue to work.
- Scalar/OpenAPI includes service permission registry endpoints.
- Migrations apply in Development/Testing without destructive data loss.

## Manual Acceptance Smoke Scenario

1. Authenticate as an administrator or service owner with `service-permissions:write`.
2. Register service `inventory-api` with owner `inventory-team` and permissions `read`, `write`, `delete`, `export`, and `approve`.
3. Confirm the service appears in `/api/admin/service-permissions/services`.
4. Confirm full permission keys appear in `/api/admin/service-permissions/catalog` as `inventory-api:read`, `inventory-api:write`, and so on.
5. Assign an active permission to a role through role management.
6. Deprecate the assigned permission and verify existing assignments remain visible while new assignment behavior is blocked or explicitly warned according to policy.
7. Attempt to retire or delete a permission with active role dependencies and verify dependency details are returned and the unsafe operation is blocked.
8. Attempt service update as a non-owner/non-admin and verify the action is denied and audited.
9. Transfer ownership as an administrator and verify owner and audit data are visible.

## Contract Verification Checklist

- Every endpoint returns `401` for unauthenticated requests.
- Mutating endpoints return `403` when the actor lacks ownership, delegation, or administrator rights.
- Duplicate service identifiers and duplicate permission keys return validation/conflict responses without partial writes.
- Validation errors use actionable `application/problem+json` responses without sensitive detail leakage.
- Stale concurrency tokens return `412`.

## Data and Migration Checklist

- Add strongly typed IDs for registered services, service permissions, delegated maintainers, and audit events where needed.
- Add EF Core configurations under `OpenIdentityStack.Infrastructure/Persistence/ServicePermissions`.
- Add unique indexes for normalized service identifiers and permission keys.
- Add indexes for owner, status, full permission key, and update timestamps.
- Add optimistic concurrency tokens.
- Ensure migrations are additive and preserve existing roles and hard-coded platform/admin permissions.

## Security Checklist

- Enforce OpenIddict bearer authentication on all endpoints.
- Define coarse RBAC permissions for registry read/write/admin operations.
- Enforce service owner, delegated maintainer, and administrator boundaries in use cases.
- Validate service identifiers and permission keys against reserved namespaces.
- Audit accepted, denied, validation-failed, and conflicting attempts.
- Avoid secrets, raw tokens, and sensitive payloads in audit records.

## Performance Checklist

- Use pagination for list/catalog endpoints.
- Use indexed filters for search, owner, status, and permission key lookups.
- Avoid N+1 loading of permissions/dependencies.
- Validate P50/P95/P99 API response targets from the constitution for representative catalog sizes.
- Confirm administrators can locate impacted roles for a removed permission within 1 minute.
