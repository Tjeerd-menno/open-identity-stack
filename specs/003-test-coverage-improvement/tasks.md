# Test Coverage Improvement Tasks

**Status**: Draft  
**Created**: 2026-01-20  
**Feature**: Improve Test Coverage to ≥85% Line Coverage

---

## Coverage Summary (Baseline)

| Assembly | Line Coverage | Target |
|----------|---------------|--------|
| OpenIdentityStack.Api | 53.7% | 80% |
| OpenIdentityStack.Application | 69.9% | 85% |
| OpenIdentityStack.Domain | 81.0% | 90% |
| OpenIdentityStack.Infrastructure | 91.0% | ✅ (Maintain) |
| **Overall** | **78.8%** | **85%** |

---

## Phase 1: Critical - Zero Coverage Areas (0%)

These have **no tests at all** and represent the highest risk areas.

### Api Layer (0% Coverage)

- [X] T001 [P] Create tests for `ExternalAuthenticationExtensions` in `tests/OpenIdentityStack.Api.Tests/Authentication/ExternalAuthenticationSetupTests.cs`
- [X] T002 [P] Create tests for `ExternalAuthenticationSetup` in `tests/OpenIdentityStack.Api.Tests/Authentication/ExternalAuthenticationSetupTests.cs`
- [X] T003 [P] Cover external login callback behavior through `AccountController`; the legacy `ExternalCallbackController` route set has been removed
- [X] T004 [P] Create tests for `RequireAllPermissionsAttribute` in `tests/OpenIdentityStack.Api.Tests/Authorization/RequireAllPermissionsAttributeTests.cs`
- [X] T005 [P] Create tests for `RequireAnyPermissionAttribute` in `tests/OpenIdentityStack.Api.Tests/Authorization/RequireAnyPermissionAttributeTests.cs`
- [X] T006 [P] Create tests for `PagedRequest`, `PagedResponse<T>`, `PagedResponseFactory` in `tests/OpenIdentityStack.Api.Tests/Common/PaginationTests.cs`

### Application Layer (0% Coverage)

- [X] T007 [P] Create tests for `NotifyClientsOfLogoutUseCase` in `tests/OpenIdentityStack.Application.Tests/Sessions/NotifyClientsOfLogoutUseCaseTests.cs`
- [X] T008 [P] Create tests for `FindUserByUpstreamIdentityQueryHandler` in `tests/OpenIdentityStack.Application.Tests/Users/FindUserByUpstreamIdentityQueryHandlerTests.cs`
- [X] T009 [P] Create tests for `UpdateProviderCommand` and `UpdateProviderUseCase` in `tests/OpenIdentityStack.Application.Tests/Federation/UpdateProviderUseCaseTests.cs`
- [X] T010 [P] Create tests for `AddGroupMappingCommand` and `AddGroupMappingUseCase` in `tests/OpenIdentityStack.Application.Tests/Groups/AddGroupMappingUseCaseTests.cs`
- [X] T011 [P] Create tests for `RemoveGroupMappingCommand` and `RemoveGroupMappingUseCase` in `tests/OpenIdentityStack.Application.Tests/Groups/RemoveGroupMappingUseCaseTests.cs`
- [X] T012 [P] Create tests for `AddCertificateCommand` and `AddCertificateUseCase` in `tests/OpenIdentityStack.Application.Tests/ServiceAccounts/AddCertificateUseCaseTests.cs`
- [X] T013 [P] Create tests for `ValidateCertificateCommand` and `ValidateCertificateUseCase` in `tests/OpenIdentityStack.Application.Tests/ServiceAccounts/ValidateCertificateUseCaseTests.cs`

### Domain Layer (0% Coverage)

- [X] T014 [P] Create tests for `ClaimMapping` and `ClaimMappingErrors` in `tests/OpenIdentityStack.Domain.Tests/Federation/ClaimMappingTests.cs`
- [X] T015 [P] Create tests for `ProviderId` strongly-typed ID in `tests/OpenIdentityStack.Domain.Tests/Common/ProviderIdTests.cs`
- [X] T015a [P] Create tests for `GroupMembership` in `tests/OpenIdentityStack.Domain.Tests/Groups/GroupMembershipTests.cs`

### Infrastructure Layer (0% Coverage)

- [X] T016 [P] Create tests for `OidcDiscoveryDocument` in `tests/OpenIdentityStack.Infrastructure.Tests/ExternalProviders/OidcDiscoveryDocumentTests.cs`
- [X] T017 [P] Create tests for `OidcProviderAdapter` in `tests/OpenIdentityStack.Infrastructure.Tests/ExternalProviders/OidcProviderAdapterTests.cs`
- [X] T018 [P] Create tests for `TokenResponse` and `UserInfoResponse` in `tests/OpenIdentityStack.Infrastructure.Tests/ExternalProviders/ExternalProviderResponseTests.cs`
- [X] T018a [P] Create tests for `AuditLogService` in `tests/OpenIdentityStack.Infrastructure.Tests/Audit/AuditLogServiceTests.cs`

---

## Phase 2: Low Coverage Areas (1-30%)

### Api Layer

- [X] T019 [P] Increase `AccountController` coverage from 16.1% to 80% in `tests/OpenIdentityStack.Api.Tests/Authentication/AccountControllerTests.cs`
  - Test Login GET/POST flows
  - Test AccessDenied view
  - Test error handling scenarios

- [X] T020 [P] Create tests for `Views_Account_AccessDenied` Razor view in `tests/OpenIdentityStack.Api.Tests/Views/AccessDeniedViewTests.cs`

- [X] T021 [P] Create tests for `Views_Account_Login` Razor view (currently 35.7%) in `tests/OpenIdentityStack.Api.Tests/Views/LoginViewTests.cs`

### Application Layer

- [X] T022 [P] Increase `GetGroupClaimsForUserQueryHandler` coverage from 21% to 90% in `tests/OpenIdentityStack.Application.Tests/Groups/GetGroupClaimsForUserQueryHandlerTests.cs`

- [X] T023 [P] Increase `GetUserGroupsQueryHandler` coverage from 26.6% to 90% in `tests/OpenIdentityStack.Application.Tests/Groups/GetUserGroupsQueryHandlerTests.cs`

- [X] T024 [P] Increase `ListGroupMappingsQueryHandler` coverage from 21% to 90% in `tests/OpenIdentityStack.Application.Tests/Groups/ListGroupMappingsQueryHandlerTests.cs`

- [X] T025 [P] Increase `ListGroupMembersQueryHandler` coverage from 19.2% to 90% in `tests/OpenIdentityStack.Application.Tests/Groups/ListGroupMembersQueryHandlerTests.cs`

- [X] T026 [P] Increase `ValidateSessionQueryHandler` coverage from 28% to 90% in `tests/OpenIdentityStack.Application.Tests/Sessions/ValidateSessionQueryHandlerTests.cs`

- [X] T027 [P] Increase `AddClientSessionUseCase` coverage from 36.8% to 90% in `tests/OpenIdentityStack.Application.Tests/Sessions/AddClientSessionUseCaseTests.cs`

### Infrastructure Layer

- [X] T028 [P] Increase `BackChannelLogoutNotifier` coverage from 4% to 80% in `tests/OpenIdentityStack.Infrastructure.Tests/Identity/BackChannelLogoutNotifierTests.cs`

- [X] T029 [P] Increase `FrontChannelLogoutService` coverage from 5.9% to 80% in `tests/OpenIdentityStack.Infrastructure.Tests/Identity/FrontChannelLogoutServiceTests.cs`

---

## Phase 3: Medium Coverage Areas (30-60%)

### Api Layer

- [X] T030 [P] Increase `AuthorizationController` coverage from 49.2% to 85% in `tests/OpenIdentityStack.Api.Tests/Authentication/AuthorizationControllerTests.cs`
  - Add tests for token exchange flows
  - Add tests for refresh token handling
  - Add tests for error scenarios

- [X] T031 [P] Increase `GroupsController` coverage from 50% to 85% in `tests/OpenIdentityStack.Api.Tests/Admin/GroupsControllerTests.cs`
  - Test group CRUD operations
  - Test member management
  - Test group mapping operations

- [X] T032 [P] Increase `ProvidersController` coverage from 50.3% to 85% in `tests/OpenIdentityStack.Api.Tests/Federation/ProvidersControllerTests.cs`
  - Test provider CRUD operations
  - Test provider configuration endpoints

### Application Layer

- [X] T033 [P] Increase `RemoveUserFromGroupUseCase` coverage from 43.7% to 90% in `tests/OpenIdentityStack.Application.Tests/Groups/RemoveUserFromGroupUseCaseTests.cs`

- [X] T034 [P] Increase `GetUserEffectiveRolesQueryHandler` coverage from 56.8% to 90% in `tests/OpenIdentityStack.Application.Tests/Users/GetUserEffectiveRolesQueryHandlerTests.cs`

- [X] T035 [P] Increase `AuditLogService` coverage from 50.5% to 80% in `tests/OpenIdentityStack.Infrastructure.Tests/Audit/AuditLogServiceTests.cs`

### Domain Layer

- [X] T036 [P] Increase `ClaimMapping` coverage from 47.2% to 90% in `tests/OpenIdentityStack.Domain.Tests/Federation/ClaimMappingTests.cs`

- [X] T037 [P] Increase `ValueObject` coverage from 47.8% to 90% in `tests/OpenIdentityStack.Domain.Tests/Common/ValueObjectTests.cs`

- [X] T038 [P] Increase `ClientSession` coverage from 43.5% to 90% in `tests/OpenIdentityStack.Domain.Tests/Sessions/ClientSessionTests.cs`

- [X] T039 [P] Increase `ClientCertificate` coverage from 57.8% to 90% in `tests/OpenIdentityStack.Domain.Tests/ServiceAccounts/ClientCertificateTests.cs`

### Infrastructure Layer

- [X] T040 [P] Increase `SeedData` coverage from 56.1% to 80% in `tests/OpenIdentityStack.Infrastructure.Tests/Persistence/SeedDataTests.cs`

---

## Phase 4: Improve Good Coverage Areas (60-80%)

### Api Layer

- [X] T041 [P] Increase `RolesController` coverage from 61.8% to 85% in `tests/OpenIdentityStack.Api.Tests/Admin/RolesControllerTests.cs`

- [X] T042 [P] Increase `RequirePermissionAttribute` coverage from 66.6% to 90% in `tests/OpenIdentityStack.Api.Tests/Common/RequirePermissionAttributeTests.cs`

- [X] T043 [P] Increase `TestSeedingController` coverage from 73.9% to 90% in `tests/OpenIdentityStack.Api.Tests/Admin/TestSeedingControllerTests.cs`

- [X] T044 [P] Increase `ServiceAccountsController` coverage from 76.5% to 90% in `tests/OpenIdentityStack.Api.Tests/ServiceAccounts/ServiceAccountsControllerTests.cs`

- [X] T045 [P] Increase `LogoutController` coverage from 77.6% to 90% in `tests/OpenIdentityStack.Api.Tests/Authentication/LogoutControllerTests.cs`

### Application Layer

- [X] T046 [P] Increase `PermissionChecker` coverage from 70% to 90% in `tests/OpenIdentityStack.Application.Tests/Authorization/PermissionCheckerTests.cs`

- [X] T047 [P] Increase `ListGroupsQueryHandler` coverage from 64.7% to 90% in `tests/OpenIdentityStack.Application.Tests/Groups/ListGroupsQueryHandlerTests.cs`

- [X] T048 [P] Increase `DeleteGroupUseCase` coverage from 75% to 90% in `tests/OpenIdentityStack.Application.Tests/Groups/DeleteGroupUseCaseTests.cs`

- [X] T049 [P] Increase `CreateGroupUseCase` coverage from 77.7% to 90% in `tests/OpenIdentityStack.Application.Tests/Groups/CreateGroupUseCaseTests.cs`

- [X] T050 [P] Increase `UnassignRoleUseCase` coverage from 78.5% to 90% in `tests/OpenIdentityStack.Application.Tests/Roles/UnassignRoleUseCaseTests.cs`

### Domain Layer

- [X] T051 [P] Increase `Group` coverage from 76% to 90% in `tests/OpenIdentityStack.Domain.Tests/Groups/GroupTests.cs`

- [X] T052 [P] Increase `GroupMembership` coverage from 64.7% to 90% in `tests/OpenIdentityStack.Domain.Tests/Groups/GroupMembershipTests.cs`

### Infrastructure Layer

- [X] T053 [P] Increase `PasswordHasher` coverage from 66.6% to 90% in `tests/OpenIdentityStack.Infrastructure.Tests/Identity/PasswordHasherTests.cs`

- [X] T054 [P] Increase `ServiceAccountValidationHandler` coverage from 64.5% to 85% in `tests/OpenIdentityStack.Infrastructure.Tests/Identity/ServiceAccountValidationHandlerTests.cs`

- [X] T055 [P] Increase `ServiceAccountRepository` coverage from 63.2% to 85% in `tests/OpenIdentityStack.Infrastructure.Tests/Persistence/ServiceAccountRepositoryTests.cs`

---

## Phase 5: Strongly-Typed IDs Coverage

Currently at 16-25% coverage. Add comprehensive tests for all strongly-typed IDs.

- [X] T056 [P] Increase `GroupId` coverage from 16.6% to 100% in `tests/OpenIdentityStack.Domain.Tests/Common/StronglyTypedIdTests.cs`
- [X] T057 [P] Increase `RoleId` coverage from 16.6% to 100% (same file as T056)
- [X] T058 [P] Increase `ServiceAccountId` coverage from 16.6% to 100% (same file as T056)
- [X] T059 [P] Increase `SessionId` coverage from 16.6% to 100% (same file as T056)
- [X] T060 [P] Increase `UserId` coverage from 25% to 100% (same file as T056)

---

## Phase 6: Domain Events Coverage

Domain events have inconsistent coverage (16-100%). Standardize to 90%+.

- [X] T061 [P] Increase `ServiceAccountDomainEvents` coverage to 90% in `tests/OpenIdentityStack.Domain.Tests/ServiceAccounts/ServiceAccountDomainEventsTests.cs`
  - `CertificateAdded` (16.6%)
  - `CertificateRevoked` (33.3%)
  - `CredentialAdded` (20%)
  - `CredentialRevoked` (33.3%)
  - `ServiceAccountDisabled` (50%)
  - `ServiceAccountEnabled` (50%)
  - `ServiceAccountUpdated` (50%)

- [X] T062 [P] Increase `UserDomainEvents` coverage to 90% in `tests/OpenIdentityStack.Domain.Tests/Users/UserDomainEventsTests.cs`
  - `UserDisabled` (66.6%)
  - `UserEmailVerified` (50%)
  - `UserEnabled` (50%)
  - `UserLoggedIn` (50%)
  - `UserPasswordChanged` (50%)

- [X] T063 [P] Increase `SessionRevokedEvent` coverage from 50% to 90% in `tests/OpenIdentityStack.Domain.Tests/Sessions/SessionRevokedEventTests.cs`

---

## Phase 7: Integration Tests for External Provider Flows

The external authentication flow has 0% coverage. Add integration tests.

- [X] T064 Create integration test for OIDC provider flow in `tests/OpenIdentityStack.Contract.Tests/Authentication/OidcProviderFlowTests.cs`
  - Test discovery document parsing
  - Test token exchange
  - Test user info retrieval

- [X] T065 Create integration test for JIT provisioning in `tests/OpenIdentityStack.Contract.Tests/Authentication/JitProvisioningTests.cs`
  - Test user creation from external claims
  - Test claim mapping

---

## Phase 8: Validation & CI Integration

- [X] T066 Add coverage threshold to CI pipeline (80% minimum)
- [X] T067 Create coverage badge for README.md
- [ ] T068 Run final coverage report and verify 85%+ overall coverage locally and in CI

---

## Dependencies

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6 → Phase 7 → Phase 8
```

All phases can be executed in parallel within themselves (all tasks marked [P]).
Cross-phase dependencies are sequential.

---

## Parallel Execution Examples

### Maximum Parallelism per Phase:

**Phase 1**: T001-T018 (18 tasks in parallel)
**Phase 2**: T019-T029 (11 tasks in parallel)
**Phase 3**: T030-T040 (11 tasks in parallel)
**Phase 4**: T041-T055 (15 tasks in parallel)
**Phase 5**: T056-T060 (5 tasks, but can be single file)
**Phase 6**: T061-T063 (3 tasks in parallel)
**Phase 7**: T064-T065 (2 tasks in parallel)
**Phase 8**: T066-T068 (sequential)

---

## Priority Order

1. **Phase 1** (Critical): 0% coverage = highest risk, no regression protection
2. **Phase 2** (High): <30% coverage = very low confidence
3. **Phase 7** (High): External provider flows = security-critical path
4. **Phase 3** (Medium): 30-60% coverage = partial protection
5. **Phase 6** (Medium): Domain events = audit trail coverage
6. **Phase 4** (Low): 60-80% coverage = reasonable baseline
7. **Phase 5** (Low): Strongly-typed IDs = utility classes
8. **Phase 8** (Final): CI integration

---

## Metrics

| Metric | Current | Target |
|--------|---------|--------|
| Line Coverage | 78.8% | 85% |
| Branch Coverage | 54.4% | 70% |
| Method Coverage | 73.7% | 85% |
| Classes with 0% Coverage | ~25 | 0 |
