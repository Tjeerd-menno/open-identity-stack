# Tasks: Service/API Permission Registry

**Input**: Design documents from `/home/runner/work/open-identity-stack/open-identity-stack/specs/copilot/add-permission-registration-feature/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/service-permission-registry.openapi.yaml`, `quickstart.md`

**Tests**: Required. The plan, research, and quickstart explicitly require TDD with failing domain, application, infrastructure, API, and contract tests before implementation.

**Organization**: Tasks are grouped by user story so each story can be implemented, tested, and delivered as an independently valuable Clean Architecture vertical slice across `Domain → Application → Infrastructure → Api`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it touches different files and does not depend on incomplete tasks in the same phase.
- **[Story]**: User story label for story-phase tasks only.
- Every task includes exact repository paths for the files or directories to change.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare the feature slice directories and test locations without adding implementation behavior.

- [X] T001 Create ServicePermissions domain directory in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Domain/ServicePermissions/`
- [X] T002 [P] Create ServicePermissions application directories in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Commands/`, `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Queries/`, `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Authorization/`, `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Validators/`, and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Dtos/`
- [X] T003 [P] Create ServicePermissions infrastructure directories in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/Persistence/ServicePermissions/` and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/ServicePermissions/`
- [X] T004 [P] Create ServicePermissions API directories in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/Requests/ServicePermissions/` and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/Responses/ServicePermissions/`
- [X] T005 [P] Create ServicePermissions test directories in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Domain.Tests/ServicePermissions/`, `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Application.Tests/ServicePermissions/`, `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Infrastructure.Tests/ServicePermissions/`, `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Api.Tests/Admin/ServicePermissions/`, and `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Contract.Tests/ServicePermissions/`
- [X] T006 [P] Add a copied implementation contract fixture from `/home/runner/work/open-identity-stack/open-identity-stack/specs/copilot/add-permission-registration-feature/contracts/service-permission-registry.openapi.yaml` to `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Contract.Tests/ServicePermissions/service-permission-registry.openapi.yaml`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Define shared abstractions, constants, and wiring seams that all user stories depend on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T007 Define `IServicePermissionRegistryRepository` interface in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/Abstractions/IServicePermissionRegistryRepository.cs`
- [X] T008 [P] Define `IServicePermissionAuthorizationService` interface in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/Abstractions/IServicePermissionAuthorizationService.cs`
- [X] T009 [P] Define `IRolePermissionDependencyReader` interface in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/Abstractions/IRolePermissionDependencyReader.cs`
- [X] T010 [P] Define `IServicePermissionAuditWriter` interface in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/Abstractions/IServicePermissionAuditWriter.cs`
- [X] T011 Add service-permission RBAC constants `Read`, `Write`, `Admin`, and `All` to `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/Authorization/Permissions.cs`
- [X] T012 [P] Define shared service-permission DTO records in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Dtos/ServicePermissionDtos.cs`
- [X] T013 [P] Define shared pagination/filter query records in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Queries/ServicePermissionQueryModels.cs`
- [X] T014 [P] Define shared command result records for registry mutations in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Commands/ServicePermissionCommandResults.cs`
- [X] T015 Register empty ServicePermissions DI extension points in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/ServiceCollectionExtensions.cs`
- [X] T016 Add ServicePermissions route mapping placeholder call to `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Program.cs`
- [X] T017 [P] Add reserved namespace configuration defaults for `users`, `roles`, `groups`, `service-accounts`, `sessions`, `providers`, `clients`, `audit-logs`, `system`, and `*` in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Validators/ReservedServicePermissionNamespaces.cs`
- [X] T018 [P] Add initial ServicePermissions contract test harness in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Contract.Tests/ServicePermissions/ServicePermissionRegistryContractTestBase.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin in priority order or in parallel where capacity allows.

---

## Phase 3: User Story 1 - Register a service and its permissions (Priority: P1) 🎯 MVP

**Goal**: Authorized service owners can register a service/API with at least one declared permission, then administrators can view that service and its active permissions for administrative and RBAC catalog use.

**Independent Test**: Register `inventory-api` with five permissions, verify atomic rejection of duplicate keys, then verify the service, owner, permissions, status, last update data, and catalog entries are returned by service-list, service-detail, and catalog queries.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [X] T019 [P] [US1] Add domain tests for service registration validation, owner requirement, duplicate permission rejection, reserved namespace rejection, full permission key generation, and atomic aggregate creation in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Domain.Tests/ServicePermissions/RegisteredServiceRegistrationTests.cs`
- [X] T020 [P] [US1] Add application tests for `RegisterServiceUseCase` success, validation failure without partial save, conflict handling, and audit result intent in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Application.Tests/ServicePermissions/RegisterServiceUseCaseTests.cs`
- [X] T021 [P] [US1] Add application query tests for listing services, service detail retrieval, and active permission catalog filtering in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Application.Tests/ServicePermissions/ServicePermissionCatalogQueryTests.cs`
- [X] T022 [P] [US1] Add infrastructure tests for persisted services, permissions, maintainers, unique indexes, full permission keys, and pagination filters in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Infrastructure.Tests/ServicePermissions/ServicePermissionRegistryRepositoryTests.cs`
- [X] T023 [P] [US1] Add API integration tests for `POST /api/admin/service-permissions/services`, `GET /api/admin/service-permissions/services`, `GET /api/admin/service-permissions/services/{serviceId}`, and `GET /api/admin/service-permissions/catalog` in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Api.Tests/Admin/ServicePermissions/RegisterServiceApiTests.cs`
- [X] T024 [P] [US1] Add OpenAPI contract tests for `registerService`, `listRegisteredServices`, `getRegisteredService`, and `listAssignablePermissionCatalog` response shapes in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Contract.Tests/ServicePermissions/RegisterServiceEndpointContractTests.cs`

### Implementation for User Story 1

- [X] T025 [P] [US1] Implement strongly typed IDs and owner value objects in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Domain/ServicePermissions/ServicePermissionIds.cs` and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Domain/ServicePermissions/ServiceOwner.cs`
- [X] T026 [P] [US1] Implement service and permission lifecycle enum types in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Domain/ServicePermissions/ServiceLifecycleStatus.cs` and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Domain/ServicePermissions/PermissionLifecycleStatus.cs`
- [X] T027 [P] [US1] Implement `ServicePermission` entity with stable key normalization, full key generation, metadata validation, assignability calculation, and immutable key behavior in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Domain/ServicePermissions/ServicePermission.cs`
- [X] T028 [P] [US1] Implement `DelegatedMaintainer` entity in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Domain/ServicePermissions/DelegatedMaintainer.cs`
- [X] T029 [US1] Implement `RegisteredService` aggregate registration factory, owner validation, duplicate permission detection, reserved namespace checks, and permission collection behavior in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Domain/ServicePermissions/RegisteredService.cs`
- [X] T030 [US1] Implement register-service command, validator, use-case interface, and use-case class in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Commands/RegisterServiceCommand.cs`, `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Validators/RegisterServiceValidator.cs`, and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Commands/RegisterServiceUseCase.cs`
- [X] T031 [P] [US1] Implement list-services and get-service query handlers in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Queries/ListRegisteredServicesQueryHandler.cs` and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Queries/GetRegisteredServiceQueryHandler.cs`
- [X] T032 [P] [US1] Implement assignable permission catalog query handler in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Queries/ListAssignablePermissionCatalogQueryHandler.cs`
- [X] T033 [US1] Add `DbSet` properties and strongly typed ID conversions for service permission registry entities in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/Persistence/OpenIdentityStackDbContext.cs`
- [X] T034 [P] [US1] Implement EF Core configurations for registered services, permissions, delegated maintainers, enum string storage, concurrency tokens, and indexes in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/Persistence/ServicePermissions/RegisteredServiceConfiguration.cs`, `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/Persistence/ServicePermissions/ServicePermissionConfiguration.cs`, and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/Persistence/ServicePermissions/DelegatedMaintainerConfiguration.cs`
- [X] T035 [US1] Implement EF Core repository for registration, list/detail, and catalog reads in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/Persistence/ServicePermissions/ServicePermissionRegistryRepository.cs`
- [ ] T036 [US1] Add additive EF Core migration for service-permission registry tables and indexes in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/Persistence/Migrations/`
- [X] T037 [US1] Implement API request and response DTOs for registration, service summary/detail, owner, maintainer, permission, and catalog responses in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/Requests/ServicePermissions/ServicePermissionRequests.cs` and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/Responses/ServicePermissions/ServicePermissionResponses.cs`
- [X] T038 [US1] Implement Minimal API endpoints and mapper methods for registration, list, detail, and catalog routes in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/ServicePermissionsApi.cs`

**Checkpoint**: User Story 1 is fully functional and testable as the MVP.

---

## Phase 4: User Story 2 - Update exposed permissions over time (Priority: P1)

**Goal**: Authorized service owners can add permissions, update permission metadata, change lifecycle status safely, and see dependency details before unsafe removal or lifecycle operations.

**Independent Test**: Start from a registered service, add a permission, update display metadata without changing stable keys, deprecate an assigned permission, verify dependency details, and verify delete-like or unsafe retire operations are blocked while existing role assignments remain visible.

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [ ] T039 [P] [US2] Add domain tests for permission add, metadata update, key immutability, lifecycle transitions, restoration rules, and blocked hard deletion in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Domain.Tests/ServicePermissions/ServicePermissionMaintenanceTests.cs`
- [ ] T040 [P] [US2] Add application tests for add-permission, update-service, update-permission, service lifecycle, permission lifecycle, dependency acknowledgement, concurrency conflict, and atomic failure behavior in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Application.Tests/ServicePermissions/ServicePermissionMaintenanceUseCaseTests.cs`
- [ ] T041 [P] [US2] Add infrastructure tests for concurrency token handling, dependency reads from existing role permissions, wildcard dependency matching, and lifecycle persistence in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Infrastructure.Tests/ServicePermissions/RolePermissionDependencyReaderTests.cs`
- [ ] T042 [P] [US2] Add API integration tests for `PATCH /api/admin/service-permissions/services/{serviceId}`, `POST /api/admin/service-permissions/services/{serviceId}/permissions`, `PATCH /api/admin/service-permissions/permissions/{permissionId}`, `POST /api/admin/service-permissions/permissions/{permissionId}/lifecycle`, `POST /api/admin/service-permissions/services/{serviceId}/lifecycle`, and `GET /api/admin/service-permissions/permissions/{permissionId}/dependencies` in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Api.Tests/Admin/ServicePermissions/ServicePermissionMaintenanceApiTests.cs`
- [ ] T043 [P] [US2] Add OpenAPI contract tests for maintenance, lifecycle, dependency, conflict, and `412 PreconditionFailed` response shapes in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Contract.Tests/ServicePermissions/ServicePermissionMaintenanceEndpointContractTests.cs`

### Implementation for User Story 2

- [ ] T044 [US2] Add metadata update, add-permission, service lifecycle, and permission lifecycle methods to `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Domain/ServicePermissions/RegisteredService.cs`
- [ ] T045 [US2] Add permission lifecycle transition methods, status timestamps, dependency-aware retire/disable guards, and restoration rules to `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Domain/ServicePermissions/ServicePermission.cs`
- [ ] T046 [P] [US2] Implement role assignment dependency read model in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Domain/ServicePermissions/RoleAssignmentDependency.cs`
- [ ] T047 [P] [US2] Implement update-service, add-permission, update-permission, change-service-lifecycle, and change-permission-lifecycle command models in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Commands/ServicePermissionMaintenanceCommands.cs`
- [ ] T048 [P] [US2] Implement validators for service metadata, permission metadata, lifecycle requests, documentation URLs, and concurrency tokens in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Validators/ServicePermissionMaintenanceValidators.cs`
- [ ] T049 [US2] Implement maintenance use cases for service metadata updates, adding permissions, permission metadata updates, service lifecycle changes, and permission lifecycle changes in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Commands/ServicePermissionMaintenanceUseCases.cs`
- [ ] T050 [P] [US2] Implement permission dependency query handler in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Queries/GetPermissionDependenciesQueryHandler.cs`
- [ ] T051 [US2] Extend repository interface with update, lifecycle, permission lookup, dependency-aware save, and concurrency methods in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/Abstractions/IServicePermissionRegistryRepository.cs`
- [ ] T052 [US2] Extend EF repository with update, lifecycle, permission lookup, dependency-aware save, and concurrency handling in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/Persistence/ServicePermissions/ServicePermissionRegistryRepository.cs`
- [ ] T053 [US2] Implement `IRolePermissionDependencyReader` over existing role permission data, exact keys, and wildcard matches in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/ServicePermissions/RolePermissionDependencyReader.cs`
- [ ] T054 [US2] Extend API request/response DTOs for maintenance, lifecycle, dependencies, conflicts, and precondition failures in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/Requests/ServicePermissions/ServicePermissionRequests.cs` and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/Responses/ServicePermissions/ServicePermissionResponses.cs`
- [ ] T055 [US2] Extend Minimal API endpoints for service update, permission add/update, lifecycle, dependency lookup, `409 Conflict`, and `412 PreconditionFailed` behavior in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/ServicePermissionsApi.cs`
- [ ] T056 [US2] Update EF Core migration snapshot for lifecycle timestamps, concurrency tokens, dependency indexes, and status indexes in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/Persistence/Migrations/OpenIdentityStackDbContextModelSnapshot.cs`

**Checkpoint**: User Story 2 is fully functional and testable independently after registering a service fixture.

---

## Phase 5: User Story 3 - Enforce ownership, validation, and security boundaries (Priority: P2)

**Goal**: Service owners, delegated maintainers, and administrators have explicit boundaries; unauthorized changes are denied; ownership transfer and emergency changes are audited without leaking sensitive details.

**Independent Test**: Attempt registration and updates as an owner, delegated maintainer, non-owner, and administrator; verify allowed actions, denied actions, ownership transfer, disabled service behavior, safe problem details, and audit records for accepted, denied, validation-failed, and conflict outcomes.

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [ ] T057 [P] [US3] Add domain tests for delegated maintainer uniqueness, owner transfer invariants, and administrator-only restoration rules in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Domain.Tests/ServicePermissions/ServicePermissionOwnershipTests.cs`
- [ ] T058 [P] [US3] Add application tests for owner, delegated maintainer, non-owner, and administrator authorization decisions in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Application.Tests/ServicePermissions/ServicePermissionAuthorizationServiceTests.cs`
- [ ] T059 [P] [US3] Add application tests for audit event creation on accepted, denied, validation-failed, conflict, ownership transfer, and lifecycle outcomes in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Application.Tests/ServicePermissions/ServicePermissionAuditTests.cs`
- [ ] T060 [P] [US3] Add infrastructure tests for persisted audit events and safe before/after JSON values in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Infrastructure.Tests/ServicePermissions/ServicePermissionAuditRepositoryTests.cs`
- [ ] T061 [P] [US3] Add API integration tests for `401`, `403`, safe validation errors, ownership transfer, delegated maintainer changes, and administrator override on `/api/admin/service-permissions/*` routes in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Api.Tests/Admin/ServicePermissions/ServicePermissionSecurityApiTests.cs`
- [ ] T062 [P] [US3] Add OpenAPI contract tests for `transferServiceOwnership`, `Unauthorized`, `Forbidden`, `ValidationProblem`, and safe `ProblemDetails` responses in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Contract.Tests/ServicePermissions/ServicePermissionSecurityEndpointContractTests.cs`

### Implementation for User Story 3

- [ ] T063 [P] [US3] Implement audit event domain record and safe audit value model in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Domain/ServicePermissions/ServicePermissionAuditEvent.cs`
- [ ] T064 [US3] Add ownership transfer, delegated maintainer replacement, administrator restoration, and denial reason behavior to `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Domain/ServicePermissions/RegisteredService.cs`
- [ ] T065 [US3] Implement `ServicePermissionAuthorizationService` for owner, delegated maintainer, and administrator override checks in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Authorization/ServicePermissionAuthorizationService.cs`
- [ ] T066 [US3] Implement audit writer and integrate audit outcomes into registration and maintenance use cases in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Commands/ServicePermissionAuditWriter.cs` and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Commands/ServicePermissionMaintenanceUseCases.cs`
- [ ] T067 [P] [US3] Implement transfer ownership command, validator, and use case in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Commands/TransferServiceOwnershipCommand.cs`
- [ ] T068 [US3] Add EF Core configuration and repository persistence for audit events in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/Persistence/ServicePermissions/ServicePermissionAuditEventConfiguration.cs` and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/Persistence/ServicePermissions/ServicePermissionRegistryRepository.cs`
- [ ] T069 [US3] Add ownership transfer request/response mapping and endpoint behavior for `POST /api/admin/service-permissions/services/{serviceId}/ownership` in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/ServicePermissionsApi.cs`
- [ ] T070 [US3] Apply OpenIddict bearer authentication, RBAC policy requirements, owner/delegation checks, and safe `ProblemDetails` mapping to all registry endpoints in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/ServicePermissionsApi.cs`

**Checkpoint**: User Story 3 is fully functional and testable independently with mocked or seeded principals.

---

## Phase 6: User Story 4 - Use registered permissions in RBAC and admin workflows (Priority: P2)

**Goal**: Registered permissions become the source of truth for service-exposed RBAC assignment workflows while existing platform/admin permission constants remain available.

**Independent Test**: Register permissions for multiple services, create/edit roles using active registered permissions, verify deprecated/disabled/retired permissions are highlighted or blocked for new assignments, and verify disabled-service permissions are no longer offered as assignable while existing assignments remain visible.

### Tests for User Story 4 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [ ] T071 [P] [US4] Add application tests for role creation and role permission updates validating active registered service permissions while preserving platform/admin permissions in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Application.Tests/ServicePermissions/RolePermissionRegistryIntegrationTests.cs`
- [ ] T072 [P] [US4] Add application tests for deprecated, disabled, retired, and disabled-service permission visibility and assignment blocking rules in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Application.Tests/ServicePermissions/PermissionCatalogAssignmentPolicyTests.cs`
- [ ] T073 [P] [US4] Add API integration tests for role creation/editing with registered permissions and catalog grouping by service/status in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Api.Tests/Admin/ServicePermissions/RbacPermissionRegistryIntegrationTests.cs`
- [ ] T074 [P] [US4] Add contract tests verifying `/api/admin/service-permissions/catalog` supports `search`, `serviceIdentifier`, `status`, `assignableOnly`, pagination, and grouped service responses for RBAC consumers in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Contract.Tests/ServicePermissions/PermissionCatalogEndpointContractTests.cs`

### Implementation for User Story 4

- [ ] T075 [US4] Implement application service for registered permission assignment policy and platform/admin permission fallback in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Authorization/RegisteredPermissionAssignmentPolicy.cs`
- [ ] T076 [US4] Integrate registered permission assignment validation into role creation in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/Roles/Commands/CreateRoleUseCase.cs`
- [ ] T077 [US4] Integrate registered permission assignment validation into role permission set/add operations in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/RolesApi.cs`
- [ ] T078 [US4] Extend role and catalog DTOs to include permission status, assignability, service identifier, and replacement guidance in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Dtos/ServicePermissionDtos.cs` and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/Responses/ServicePermissions/ServicePermissionResponses.cs`
- [ ] T079 [US4] Extend catalog query handler to include disabled-service visibility, deprecated/disabled/retired status indicators, and assignable-only filtering in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/Queries/ListAssignablePermissionCatalogQueryHandler.cs`
- [ ] T080 [US4] Extend repository catalog queries for indexed search by service, owner, status, full permission key, and update timestamp in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/Persistence/ServicePermissions/ServicePermissionRegistryRepository.cs`
- [ ] T081 [US4] Add warnings for unavailable registered permissions in role response mapping in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/Responses/RoleResponses.cs` and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/RolesApi.cs`
- [ ] T082 [US4] Register permission assignment policy and catalog query dependencies in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/ServiceCollectionExtensions.cs`
- [ ] T083 [US4] Update API OpenAPI metadata for catalog and role permission validation errors in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/ServicePermissionsApi.cs` and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/RolesApi.cs`

**Checkpoint**: User Story 4 is fully functional and testable independently with registered service fixtures and existing role APIs.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Validate end-to-end behavior, performance, documentation, and maintainability after desired user stories are complete.

- [ ] T084 [P] Add quickstart smoke scenario documentation for the implemented API in `/home/runner/work/open-identity-stack/open-identity-stack/docs/service-permission-registry.md`
- [ ] T085 [P] Add performance tests for service list, permission catalog search, and dependency lookup latency targets in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Api.Tests/Performance/ServicePermissionRegistryLatencyTests.cs`
- [ ] T086 [P] Add security regression tests for sensitive error disclosure and audit payload sanitization in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Api.Tests/Admin/ServicePermissions/ServicePermissionSecurityRegressionTests.cs`
- [ ] T087 Review and refactor ServicePermissions code for Clean Architecture dependency direction in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Domain/ServicePermissions/`, `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/ServicePermissions/`, `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Infrastructure/ServicePermissions/`, and `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Api/Admin/ServicePermissionsApi.cs`
- [ ] T088 Run domain test suite and fix regressions in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Domain.Tests/ServicePermissions/`
- [ ] T089 Run application, infrastructure, API, and contract test suites and fix regressions in `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Application.Tests/ServicePermissions/`, `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Infrastructure.Tests/ServicePermissions/`, `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Api.Tests/Admin/ServicePermissions/`, and `/home/runner/work/open-identity-stack/open-identity-stack/tests/OpenIdentityStack.Contract.Tests/ServicePermissions/`
- [ ] T090 Validate Aspire/OpenAPI smoke checklist from quickstart against `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.AppHost/` and `/home/runner/work/open-identity-stack/open-identity-stack/specs/copilot/add-permission-registration-feature/quickstart.md`
- [ ] T091 Update release notes and migration notes for registry adoption in `/home/runner/work/open-identity-stack/open-identity-stack/docs/service-permission-registry.md`

---

## Dependencies & Execution Order

### Phase Dependencies

1. **Phase 1: Setup** has no dependencies and can start immediately.
2. **Phase 2: Foundational** depends on Phase 1 and blocks every user story.
3. **Phase 3: US1 (P1 MVP)** depends on Phase 2 and should be completed first for the minimum usable registry.
4. **Phase 4: US2 (P1)** depends on Phase 2 and can be started in parallel with US1 only if fixtures/mocks replace persisted US1 behavior; for safest delivery, complete after US1.
5. **Phase 5: US3 (P2)** depends on Phase 2 and can be implemented after US1 registration surfaces exist; it hardens authorization/audit behavior across US1 and US2.
6. **Phase 6: US4 (P2)** depends on Phase 2 and is most valuable after US1 catalog behavior exists; it integrates registered permissions with existing role workflows.
7. **Phase 7: Polish** depends on all user stories selected for release.

### User Story Dependencies

| User Story | Priority | Depends On | Can Run In Parallel With | Notes |
|------------|----------|------------|--------------------------|-------|
| US1 Register service and permissions | P1 | Phase 2 | US2 with mocks, US3 with security stubs | MVP scope and first release candidate |
| US2 Update exposed permissions | P1 | Phase 2; preferably US1 repository/API foundation | US3 | Uses existing registered service fixtures |
| US3 Ownership/security/audit | P2 | Phase 2; benefits from US1 mutation endpoints | US2, US4 | Cross-cuts mutation use cases but remains testable with stubs |
| US4 RBAC/admin consumption | P2 | Phase 2; benefits from US1 catalog | US3 | Preserves platform/admin hard-coded permissions while using registry for service permissions |

### Within Each User Story

1. Write tests first and verify they fail.
2. Implement domain model behavior.
3. Implement application commands, queries, validators, authorization, and DTO mapping.
4. Implement infrastructure persistence, dependency readers, migrations, and DI.
5. Implement API request/response DTOs, endpoints, authorization, and OpenAPI metadata.
6. Run the story-specific tests and validate the independent test criteria before moving to the next story.

---

## Parallel Execution Examples

### User Story 1

```bash
# After Phase 2, tests can be created in parallel:
Task T019: "Add domain tests in tests/OpenIdentityStack.Domain.Tests/ServicePermissions/RegisteredServiceRegistrationTests.cs"
Task T020: "Add application registration tests in tests/OpenIdentityStack.Application.Tests/ServicePermissions/RegisterServiceUseCaseTests.cs"
Task T021: "Add catalog query tests in tests/OpenIdentityStack.Application.Tests/ServicePermissions/ServicePermissionCatalogQueryTests.cs"
Task T022: "Add repository tests in tests/OpenIdentityStack.Infrastructure.Tests/ServicePermissions/ServicePermissionRegistryRepositoryTests.cs"
Task T023: "Add API tests in tests/OpenIdentityStack.Api.Tests/Admin/ServicePermissions/RegisterServiceApiTests.cs"
Task T024: "Add contract tests in tests/OpenIdentityStack.Contract.Tests/ServicePermissions/RegisterServiceEndpointContractTests.cs"

# Then independent model pieces can be created in parallel:
Task T025: "Implement IDs and owner value objects"
Task T026: "Implement lifecycle enums"
Task T027: "Implement ServicePermission entity"
Task T028: "Implement DelegatedMaintainer entity"
```

### User Story 2

```bash
Task T039: "Add domain maintenance tests"
Task T040: "Add application maintenance tests"
Task T041: "Add dependency reader infrastructure tests"
Task T042: "Add maintenance API integration tests"
Task T043: "Add maintenance contract tests"

Task T046: "Implement RoleAssignmentDependency read model"
Task T047: "Implement maintenance command models"
Task T048: "Implement maintenance validators"
Task T050: "Implement dependency query handler"
```

### User Story 3

```bash
Task T057: "Add ownership domain tests"
Task T058: "Add authorization service tests"
Task T059: "Add audit application tests"
Task T060: "Add audit persistence tests"
Task T061: "Add security API tests"
Task T062: "Add security contract tests"

Task T063: "Implement audit event domain record"
Task T067: "Implement transfer ownership command, validator, and use case"
```

### User Story 4

```bash
Task T071: "Add role-registry integration tests"
Task T072: "Add assignment policy tests"
Task T073: "Add RBAC API integration tests"
Task T074: "Add catalog contract tests"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: User Story 1.
4. **STOP and VALIDATE**: Run domain, application, infrastructure, API, and contract tests for US1 only.
5. Demonstrate registering `inventory-api` with five permissions and verifying list/detail/catalog visibility.

### Incremental Delivery

1. Deliver US1 to replace hard-coded service permission additions with registration and catalog visibility.
2. Add US2 to support safe ongoing permission maintenance and dependency review.
3. Add US3 to enforce complete ownership, delegated maintainer, administrator override, and audit behavior.
4. Add US4 to make registered permissions authoritative in existing RBAC role workflows.
5. Finish Phase 7 polish for performance, security regression coverage, documentation, and release notes.

### Parallel Team Strategy

1. Team completes Setup and Foundational phases together.
2. After Phase 2:
   - Developer A: US1 registration/catalog MVP.
   - Developer B: US2 maintenance and lifecycle using repository/test fixtures.
   - Developer C: US3 authorization/audit hardening.
   - Developer D: US4 RBAC/catalog integration after the US1 catalog contract stabilizes.
3. Merge in dependency order: US1 → US2 → US3/US4 → Polish.

## Notes

- `[P]` tasks are parallelizable because they touch distinct files and do not depend on incomplete tasks in the same phase.
- `[US1]`, `[US2]`, `[US3]`, and `[US4]` labels map directly to user stories in `/home/runner/work/open-identity-stack/open-identity-stack/specs/copilot/add-permission-registration-feature/spec.md`.
- Existing platform/admin constants in `/home/runner/work/open-identity-stack/open-identity-stack/src/OpenIdentityStack.Application/Authorization/Permissions.cs` remain available; the registry is authoritative for service-exposed permissions.
- Migrations must be additive and preserve existing roles, role assignments, OpenIddict data, and platform/admin permissions.
- Audit records must avoid secrets, raw tokens, and unnecessary sensitive personal data.
