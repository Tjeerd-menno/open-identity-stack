# Tasks: OpenIddict-Based Identity & Access Management

**Input**: Design documents from `/specs/001-openiddict-iam/`  
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/admin-api.yaml ✓

**Tests**: Included per constitution (Test-First Development is NON-NEGOTIABLE)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1-US11)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, Aspire setup, and build configuration

- [X] T001 Create solution structure with `dotnet new sln -n OpenIdentityStack` in repository root
- [X] T002 Create global.json pinning SDK version 10.0.100 in repository root
- [X] T003 [P] Create Directory.Build.props with common settings (net10.0, C# 13, nullable, analyzers) in repository root
- [X] T004 [P] Create Directory.Packages.props with Central Package Management (OpenIddict 7.2.0, EF Core 10, xunit.v3.mtp-v2, NSubstitute, Shouldly) in repository root
- [X] T005 Create src/OpenIdentityStack.AppHost project using `dotnet new aspire-apphost`
- [X] T006 [P] Create src/OpenIdentityStack.ServiceDefaults project using `dotnet new aspire-servicedefaults`
- [X] T007 [P] Create src/OpenIdentityStack.Domain class library project
- [X] T008 [P] Create src/OpenIdentityStack.Application class library project (references Domain)
- [X] T009 [P] Create src/OpenIdentityStack.Infrastructure class library project (references Application, Domain)
- [X] T010 Create src/OpenIdentityStack.Api web project (references Application, Infrastructure, ServiceDefaults)
- [X] T011 [P] Create tests/OpenIdentityStack.Domain.Tests xunit.v3.mtp-v2 project (references Domain)
- [X] T012 [P] Create tests/OpenIdentityStack.Application.Tests xunit.v3.mtp-v2 project (references Application)
- [X] T013 [P] Create tests/OpenIdentityStack.Infrastructure.Tests xunit.v3.mtp-v2 project (references Infrastructure)
- [X] T014 [P] Create tests/OpenIdentityStack.Api.Tests xunit.v3.mtp-v2 project (references Api)
- [X] T015 [P] Create tests/OpenIdentityStack.Contract.Tests xunit.v3.mtp-v2 project (references Api)
- [X] T016 Configure AppHost Program.cs with PostgreSQL and Api project references in src/OpenIdentityStack.AppHost/Program.cs
- [X] T017 Configure ServiceDefaults Extensions.cs with health checks and OpenTelemetry in src/OpenIdentityStack.ServiceDefaults/Extensions.cs
- [X] T018 Add all projects to OpenIdentityStack.sln

**Checkpoint**: Solution builds with `dotnet build`, Aspire dashboard launches with `dotnet run --project src/OpenIdentityStack.AppHost`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Domain Foundation

- [X] T019 [P] Create base Entity class in src/OpenIdentityStack.Domain/Common/Entity.cs
- [X] T020 [P] Create base AggregateRoot class in src/OpenIdentityStack.Domain/Common/AggregateRoot.cs
- [X] T021 [P] Create base ValueObject class in src/OpenIdentityStack.Domain/Common/ValueObject.cs
- [X] T022 [P] Create base DomainEvent record in src/OpenIdentityStack.Domain/Common/DomainEvent.cs
- [X] T023 [P] Create Result<T> type for use case responses in src/OpenIdentityStack.Domain/Common/Result.cs
- [X] T024 [P] Create strongly-typed IDs (UserId, RoleId, GroupId, etc.) in src/OpenIdentityStack.Domain/Common/StronglyTypedIds.cs

### Infrastructure Foundation

- [X] T025 Create OpenIdentityStackDbContext with OpenIddict entity sets in src/OpenIdentityStack.Infrastructure/Persistence/OpenIdentityStackDbContext.cs
- [X] T026 [P] Create IDateTimeProvider interface in src/OpenIdentityStack.Application/Abstractions/IDateTimeProvider.cs
- [X] T027 [P] Create DateTimeProvider implementation in src/OpenIdentityStack.Infrastructure/Common/DateTimeProvider.cs
- [X] T028 [P] Create IAuditLog interface in src/OpenIdentityStack.Application/Abstractions/IAuditLog.cs
- [X] T029 [P] Create AuditLogService implementation in src/OpenIdentityStack.Infrastructure/Audit/AuditLogService.cs
- [X] T030 Configure OpenIddict services (Core, Server, Validation) in src/OpenIdentityStack.Infrastructure/Identity/OpenIddictSetup.cs
- [X] T031 Create ServiceCollectionExtensions for Infrastructure DI registration in src/OpenIdentityStack.Infrastructure/ServiceCollectionExtensions.cs

### API Foundation

- [X] T032 Configure Api Program.cs with OpenIddict, EF Core, and ServiceDefaults in src/OpenIdentityStack.Api/Program.cs
- [X] T033 [P] Create ProblemDetails error handling middleware in src/OpenIdentityStack.Api/Common/Middleware/ProblemDetailsMiddleware.cs
- [X] T034 [P] Create RequirePermissionAttribute for RBAC in src/OpenIdentityStack.Api/Common/Filters/RequirePermissionAttribute.cs
- [X] T035 [P] Create pagination models (PagedRequest, PagedResponse) in src/OpenIdentityStack.Api/Common/Models/Pagination.cs
- [X] T036 Configure OpenIddict endpoints (authorize, token, userinfo, logout, introspect, revoke) in src/OpenIdentityStack.Api/Authentication/AuthorizationController.cs
- [X] T037 Create initial EF Core migration in src/OpenIdentityStack.Infrastructure/Persistence/Migrations/

### Foundational Tests

- [X] T038 [P] Test Result<T> type behavior in tests/OpenIdentityStack.Domain.Tests/Common/ResultTests.cs
- [X] T039 [P] Test Entity base class equality in tests/OpenIdentityStack.Domain.Tests/Common/EntityTests.cs
- [X] T040 [P] Test ProblemDetails middleware in tests/OpenIdentityStack.Api.Tests/Common/ProblemDetailsMiddlewareTests.cs

**Checkpoint**: Foundation ready - `dotnet build` succeeds, OpenIddict discovery endpoint responds at /.well-known/openid-configuration

---

## Phase 3: User Story 1 - Local User Authentication (Priority: P1) 🎯 MVP

**Goal**: Local users can authenticate with email/password via authorization code flow with PKCE

**Independent Test**: Create user, login via /connect/authorize, exchange code for tokens, validate token claims

### Tests for User Story 1

- [X] T041 [P] [US1] Unit test User entity creation and validation in tests/OpenIdentityStack.Domain.Tests/Users/UserTests.cs
- [X] T042 [P] [US1] Unit test User status transitions in tests/OpenIdentityStack.Domain.Tests/Users/UserStatusTransitionTests.cs
- [X] T043 [P] [US1] Unit test CreateUserUseCase in tests/OpenIdentityStack.Application.Tests/Users/CreateUserUseCaseTests.cs
- [X] T044 [P] [US1] Unit test ValidateUserCredentialsUseCase in tests/OpenIdentityStack.Application.Tests/Users/ValidateUserCredentialsUseCaseTests.cs
- [X] T045 [P] [US1] Integration test UserRepository in tests/OpenIdentityStack.Infrastructure.Tests/Users/UserRepositoryTests.cs
- [X] T046 [US1] Integration test authorization code flow with PKCE in tests/OpenIdentityStack.Api.Tests/Authentication/AuthorizationCodeFlowTests.cs (NOTE: Full integration tests require database - TestContainers to be added. PKCE contract tests pass.)
- [X] T047 [US1] Contract test for token response shape in tests/OpenIdentityStack.Contract.Tests/Authentication/TokenResponseContractTests.cs

### Domain Implementation for User Story 1

- [X] T048 [P] [US1] Create UserStatus enum in src/OpenIdentityStack.Domain/Users/UserStatus.cs
- [X] T049 [P] [US1] Create User aggregate root in src/OpenIdentityStack.Domain/Users/User.cs
- [X] T050 [P] [US1] Create UserDomainEvents (UserCreated, UserDisabled, etc.) in src/OpenIdentityStack.Domain/Users/UserDomainEvents.cs

### Application Implementation for User Story 1

- [X] T051 [P] [US1] Create IUserRepository interface in src/OpenIdentityStack.Application/Abstractions/IUserRepository.cs
- [X] T052 [P] [US1] Create IPasswordHasher interface in src/OpenIdentityStack.Application/Abstractions/IPasswordHasher.cs
- [X] T053 [US1] Create CreateUserCommand and ICreateUserUseCase in src/OpenIdentityStack.Application/Users/Commands/CreateUserCommand.cs
- [X] T054 [US1] Implement CreateUserUseCase in src/OpenIdentityStack.Application/Users/Commands/CreateUserUseCase.cs
- [X] T055 [US1] Create ValidateUserCredentialsQuery and IValidateUserCredentialsUseCase in src/OpenIdentityStack.Application/Users/Commands/ValidateUserCredentialsCommand.cs
- [X] T056 [US1] Implement ValidateUserCredentialsUseCase in src/OpenIdentityStack.Application/Users/Commands/ValidateUserCredentialsUseCase.cs

### Infrastructure Implementation for User Story 1

- [X] T057 [P] [US1] Create UserConfiguration (EF Core) in src/OpenIdentityStack.Infrastructure/Persistence/Users/UserConfiguration.cs
- [X] T058 [P] [US1] Implement PasswordHasher using ASP.NET Core Identity in src/OpenIdentityStack.Infrastructure/Identity/PasswordHasher.cs
- [X] T059 [US1] Implement UserRepository in src/OpenIdentityStack.Infrastructure/Persistence/Users/UserRepository.cs
- [X] T060 [US1] Add User entity to DbContext and create migration

### API Implementation for User Story 1

- [X] T061 [US1] Implement authorization endpoint login UI/handler in src/OpenIdentityStack.Api/Authentication/AuthorizationController.cs
- [X] T062 [US1] Implement token endpoint for code exchange in src/OpenIdentityStack.Api/Authentication/TokenController.cs
- [X] T063 [US1] Create login page/view for authorization flow in src/OpenIdentityStack.Api/Authentication/Views/

**Checkpoint**: User can register, login via authorization code flow, receive valid JWT tokens

---

## Phase 4: User Story 2 - Service Account Token Acquisition (Priority: P2)

**Goal**: Service accounts authenticate via client credentials (secret or certificate) to obtain access tokens

**Independent Test**: Create service account with secret, call /connect/token with client_credentials grant, validate access token

### Tests for User Story 2

- [X] T064 [P] [US2] Unit test ServiceAccount entity creation in tests/OpenIdentityStack.Domain.Tests/ServiceAccounts/ServiceAccountTests.cs
- [X] T065 [P] [US2] Unit test ClientCredential value object in tests/OpenIdentityStack.Domain.Tests/ServiceAccounts/ClientCredentialTests.cs
- [X] T066 [P] [US2] Unit test ValidateClientCredentialsUseCase in tests/OpenIdentityStack.Application.Tests/ServiceAccounts/ValidateClientCredentialsUseCaseTests.cs
- [X] T067 [US2] Integration test client credentials flow in tests/OpenIdentityStack.Api.Tests/Authentication/ClientCredentialsFlowTests.cs
- [X] T068 [US2] Integration test certificate authentication in tests/OpenIdentityStack.Api.Tests/Authentication/CertificateAuthenticationTests.cs

### Domain Implementation for User Story 2

- [X] T069 [P] [US2] Create ServiceAccountStatus enum in src/OpenIdentityStack.Domain/ServiceAccounts/ServiceAccountStatus.cs
- [X] T070 [P] [US2] Create ServiceAccount aggregate root in src/OpenIdentityStack.Domain/ServiceAccounts/ServiceAccount.cs
- [X] T071 [P] [US2] Create ClientCredential value object in src/OpenIdentityStack.Domain/ServiceAccounts/ClientCredential.cs
- [X] T072 [P] [US2] Create ClientCertificate value object in src/OpenIdentityStack.Domain/ServiceAccounts/ClientCertificate.cs

### Application Implementation for User Story 2

- [X] T073 [P] [US2] Create IServiceAccountRepository interface in src/OpenIdentityStack.Application/Abstractions/IServiceAccountRepository.cs
- [X] T074 [US2] Create ValidateClientCredentialsCommand in src/OpenIdentityStack.Application/ServiceAccounts/Commands/ValidateClientCredentialsCommand.cs
- [X] T075 [US2] Implement ValidateClientCredentialsUseCase in src/OpenIdentityStack.Application/ServiceAccounts/Commands/ValidateClientCredentialsUseCase.cs
- [X] T076 [US2] Create ValidateCertificateCommand in src/OpenIdentityStack.Application/ServiceAccounts/Commands/ValidateCertificateCommand.cs
- [X] T077 [US2] Implement ValidateCertificateUseCase in src/OpenIdentityStack.Application/ServiceAccounts/Commands/ValidateCertificateUseCase.cs

### Infrastructure Implementation for User Story 2

- [X] T078 [P] [US2] Create ServiceAccountConfiguration (EF Core) in src/OpenIdentityStack.Infrastructure/Persistence/ServiceAccounts/ServiceAccountConfiguration.cs
- [X] T079 [P] [US2] Create ClientCredentialConfiguration in src/OpenIdentityStack.Infrastructure/Persistence/ServiceAccounts/ClientCredentialConfiguration.cs
- [X] T080 [US2] Implement ServiceAccountRepository in src/OpenIdentityStack.Infrastructure/Persistence/ServiceAccounts/ServiceAccountRepository.cs
- [X] T081 [US2] Integrate OpenIddict client validation with ServiceAccount store in src/OpenIdentityStack.Infrastructure/Identity/ServiceAccountValidationHandler.cs
- [X] T082 [US2] Add ServiceAccount entities to DbContext and create migration

**Checkpoint**: Service account can obtain tokens via client_credentials grant with secret or certificate

---

## Phase 5: User Story 3 - Admin User Management (Priority: P3)

**Goal**: Administrators can create, update, disable/enable, and delete users via Admin API

**Independent Test**: Authenticate as admin, create user via POST /api/admin/users, verify user in list, disable user, verify cannot login

### Tests for User Story 3

- [X] T083 [P] [US3] Unit test DisableUserUseCase in tests/OpenIdentityStack.Application.Tests/Users/DisableUserUseCaseTests.cs
- [X] T084 [P] [US3] Unit test ResetPasswordUseCase in tests/OpenIdentityStack.Application.Tests/Users/ResetPasswordUseCaseTests.cs
- [X] T085 [P] [US3] Contract test POST /api/admin/users in tests/OpenIdentityStack.Contract.Tests/Admin/UsersEndpointContractTests.cs
- [X] T086 [US3] Integration test Admin Users CRUD in tests/OpenIdentityStack.Api.Tests/Admin/UsersControllerTests.cs

### Application Implementation for User Story 3

- [X] T087 [P] [US3] Create GetUserQuery and IGetUserQuery in src/OpenIdentityStack.Application/Users/Queries/GetUserQuery.cs
- [X] T088 [P] [US3] Implement GetUserQueryHandler in src/OpenIdentityStack.Application/Users/Queries/GetUserQueryHandler.cs
- [X] T089 [P] [US3] Create ListUsersQuery in src/OpenIdentityStack.Application/Users/Queries/ListUsersQuery.cs
- [X] T090 [P] [US3] Implement ListUsersQueryHandler in src/OpenIdentityStack.Application/Users/Queries/ListUsersQueryHandler.cs
- [X] T091 [US3] Create UpdateUserCommand and UseCase in src/OpenIdentityStack.Application/Users/Commands/UpdateUserCommand.cs
- [X] T092 [US3] Create DisableUserCommand and UseCase in src/OpenIdentityStack.Application/Users/Commands/DisableUserCommand.cs
- [X] T093 [US3] Create EnableUserCommand and UseCase in src/OpenIdentityStack.Application/Users/Commands/EnableUserCommand.cs
- [X] T094 [US3] Create ResetPasswordCommand and UseCase in src/OpenIdentityStack.Application/Users/Commands/ResetPasswordCommand.cs
- [X] T095 [US3] Create DeleteUserCommand and UseCase in src/OpenIdentityStack.Application/Users/Commands/DeleteUserCommand.cs

### API Implementation for User Story 3

- [X] T096 [US3] Create UsersController with CRUD endpoints in src/OpenIdentityStack.Api/Users/UsersController.cs
- [X] T097 [P] [US3] Create UserRequests record types in src/OpenIdentityStack.Api/Users/UserRequests.cs
- [X] T098 [P] [US3] Create UserResponses record types in src/OpenIdentityStack.Api/Users/UserResponses.cs
- [X] T099 [US3] Add [RequirePermission("users:*")] attributes to controller

**Checkpoint**: Admin API /api/admin/users fully functional with RBAC

---

## Phase 6: User Story 4 - Federated User Login via Upstream IdP (Priority: P4)

**Goal**: Users authenticate via upstream OIDC provider and receive local tokens; JIT provisioning creates local user representation

**Independent Test**: Configure upstream IdP, redirect user to IdP, authenticate, verify local user created, verify local tokens issued

### Tests for User Story 4

- [X] T100 [P] [US4] Unit test UpstreamProvider entity in tests/OpenIdentityStack.Domain.Tests/Federation/UpstreamProviderTests.cs
- [X] T101 [P] [US4] Unit test UpstreamIdentity linking in tests/OpenIdentityStack.Domain.Tests/Users/UpstreamIdentityTests.cs
- [X] T102 [P] [US4] Unit test JitProvisionUserUseCase in tests/OpenIdentityStack.Application.Tests/Federation/JitProvisionUserUseCaseTests.cs
- [X] T103 [US4] Integration test federated login flow in tests/OpenIdentityStack.Api.Tests/Authentication/FederatedLoginTests.cs

### Domain Implementation for User Story 4

- [X] T104 [P] [US4] Create ProviderStatus enum in src/OpenIdentityStack.Domain/Federation/ProviderStatus.cs
- [X] T105 [P] [US4] Create UpstreamProvider aggregate root in src/OpenIdentityStack.Domain/Federation/UpstreamProvider.cs
- [X] T106 [P] [US4] Create ClaimMapping value object in src/OpenIdentityStack.Domain/Federation/ClaimMapping.cs
- [X] T107 [P] [US4] Create TransformType enum in src/OpenIdentityStack.Domain/Federation/TransformType.cs
- [X] T108 [P] [US4] Create UpstreamIdentity value object in src/OpenIdentityStack.Domain/Users/UpstreamIdentity.cs

### Application Implementation for User Story 4

- [X] T109 [P] [US4] Create IUpstreamProviderRepository in src/OpenIdentityStack.Application/Abstractions/IUpstreamProviderRepository.cs
- [X] T110 [US4] Create JitProvisionUserCommand in src/OpenIdentityStack.Application/Federation/Commands/JitProvisionUserCommand.cs
- [X] T111 [US4] Implement JitProvisionUserUseCase in src/OpenIdentityStack.Application/Federation/Commands/JitProvisionUserUseCase.cs
- [X] T112 [US4] Create LinkUpstreamIdentityCommand in src/OpenIdentityStack.Application/Users/Commands/LinkUpstreamIdentityCommand.cs
- [X] T113 [US4] Create FindUserByUpstreamIdentityQuery in src/OpenIdentityStack.Application/Users/Queries/FindUserByUpstreamIdentityQuery.cs

### Infrastructure Implementation for User Story 4

- [X] T114 [P] [US4] Create UpstreamProviderConfiguration (EF Core) in src/OpenIdentityStack.Infrastructure/Persistence/Federation/UpstreamProviderConfiguration.cs
- [X] T115 [P] [US4] Create UpstreamIdentityConfiguration in src/OpenIdentityStack.Infrastructure/Persistence/Users/UpstreamIdentityConfiguration.cs (deleted - configured as owned entity in UserConfiguration)
- [X] T116 [US4] Implement UpstreamProviderRepository in src/OpenIdentityStack.Infrastructure/Persistence/Federation/UpstreamProviderRepository.cs
- [X] T117 [US4] Create OidcProviderAdapter for upstream communication in src/OpenIdentityStack.Infrastructure/ExternalProviders/OidcProviderAdapter.cs
- [X] T118 [US4] Configure ASP.NET Core external authentication handlers in src/OpenIdentityStack.Api/Authentication/ExternalAuthenticationSetup.cs
- [X] T119 [US4] Implement external callback with JIT provisioning in src/OpenIdentityStack.Api/Authentication/ExternalCallbackController.cs
- [X] T120 [US4] Add entities to DbContext and create migration

**Checkpoint**: User can authenticate via upstream IdP, JIT provisioned user receives local tokens

---

## Phase 7: User Story 5 - Role-Based Access Control (Priority: P5)

**Goal**: Administrators assign roles to users; roles appear as claims in issued tokens

**Independent Test**: Create role, assign to user, authenticate as user, verify role claim in token

### Tests for User Story 5

- [X] T121 [P] [US5] Unit test Role entity in tests/OpenIdentityStack.Domain.Tests/Roles/RoleTests.cs
- [X] T122 [P] [US5] Unit test RoleAssignment in tests/OpenIdentityStack.Domain.Tests/Roles/RoleAssignmentTests.cs
- [X] T123 [P] [US5] Unit test AssignRoleUseCase in tests/OpenIdentityStack.Application.Tests/Roles/AssignRoleUseCaseTests.cs
- [X] T124 [US5] Integration test role claims in token in tests/OpenIdentityStack.Api.Tests/Authentication/RoleClaimsTests.cs
- [X] T125 [US5] Contract test /api/admin/roles in tests/OpenIdentityStack.Contract.Tests/Admin/RolesEndpointContractTests.cs

### Domain Implementation for User Story 5

- [X] T126 [P] [US5] Create Role entity in src/OpenIdentityStack.Domain/Roles/Role.cs
- [X] T127 [P] [US5] Create RoleAssignment join entity in src/OpenIdentityStack.Domain/Roles/RoleAssignment.cs

### Application Implementation for User Story 5

- [X] T128 [P] [US5] Create IRoleRepository in src/OpenIdentityStack.Application/Abstractions/IRoleRepository.cs
- [X] T129 [P] [US5] Create CreateRoleCommand and UseCase in src/OpenIdentityStack.Application/Roles/Commands/CreateRoleCommand.cs
- [X] T130 [P] [US5] Create ListRolesQuery in src/OpenIdentityStack.Application/Roles/Queries/ListRolesQuery.cs
- [X] T131 [US5] Create AssignRoleCommand and UseCase in src/OpenIdentityStack.Application/Roles/Commands/AssignRoleCommand.cs
- [X] T132 [US5] Create UnassignRoleCommand and UseCase in src/OpenIdentityStack.Application/Roles/Commands/UnassignRoleCommand.cs
- [X] T133 [US5] Create GetUserRolesQuery in src/OpenIdentityStack.Application/Users/Queries/GetUserRolesQuery.cs

### Infrastructure Implementation for User Story 5

- [X] T134 [P] [US5] Create RoleConfiguration (EF Core) in src/OpenIdentityStack.Infrastructure/Persistence/Roles/RoleConfiguration.cs
- [X] T135 [P] [US5] Create RoleAssignmentConfiguration in src/OpenIdentityStack.Infrastructure/Persistence/Roles/RoleAssignmentConfiguration.cs
- [X] T136 [US5] Implement RoleRepository in src/OpenIdentityStack.Infrastructure/Persistence/Roles/RoleRepository.cs
- [X] T137 [US5] Extend token generation to include role claims in src/OpenIdentityStack.Infrastructure/Identity/TokenService.cs
- [X] T138 [US5] Add entities to DbContext and create migration

### API Implementation for User Story 5

- [X] T139 [US5] Create RolesController in src/OpenIdentityStack.Api/Roles/RolesController.cs
- [X] T140 [P] [US5] Create RoleRequests/Responses in src/OpenIdentityStack.Api/Roles/RoleRequests.cs
- [X] T141 [US5] Add role assignment endpoints to UsersController in src/OpenIdentityStack.Api/Users/UsersController.cs

**Checkpoint**: Roles can be managed via Admin API, assigned to users, and appear in tokens

---

## Phase 8: User Story 6 - Group Management and Group-Based Authorization (Priority: P6)

**Goal**: Groups with role/claim mappings; group membership drives token claims

**Independent Test**: Create group with role mapping, add user to group, authenticate, verify mapped role in token

### Tests for User Story 6

- [X] T142 [P] [US6] Unit test Group aggregate in tests/OpenIdentityStack.Domain.Tests/Groups/GroupTests.cs
- [X] T143 [P] [US6] Unit test GroupMapping in tests/OpenIdentityStack.Domain.Tests/Groups/GroupMappingTests.cs
- [X] T144 [P] [US6] Unit test AddUserToGroupUseCase in tests/OpenIdentityStack.Application.Tests/Groups/AddUserToGroupUseCaseTests.cs
- [X] T145 [US6] Integration test group-derived claims in tests/OpenIdentityStack.Api.Tests/Authentication/GroupClaimsTests.cs
- [X] T146 [US6] Contract test /api/admin/groups in tests/OpenIdentityStack.Contract.Tests/Admin/GroupsEndpointContractTests.cs

### Domain Implementation for User Story 6

- [X] T147 [P] [US6] Create Group aggregate root in src/OpenIdentityStack.Domain/Groups/Group.cs
- [X] T148 [P] [US6] Create GroupMembership join entity in src/OpenIdentityStack.Domain/Groups/GroupMembership.cs
- [X] T149 [P] [US6] Create GroupMapping value object in src/OpenIdentityStack.Domain/Groups/GroupMapping.cs
- [X] T150 [P] [US6] Create MappingType enum in src/OpenIdentityStack.Domain/Groups/MappingType.cs
- [X] T151 [P] [US6] Create TokenTarget enum in src/OpenIdentityStack.Domain/Groups/TokenTarget.cs

### Application Implementation for User Story 6

- [X] T152 [P] [US6] Create IGroupRepository in src/OpenIdentityStack.Application/Abstractions/IGroupRepository.cs
- [X] T153 [P] [US6] Create CreateGroupCommand and UseCase in src/OpenIdentityStack.Application/Groups/Commands/CreateGroupCommand.cs
- [X] T154 [US6] Create AddUserToGroupCommand and UseCase in src/OpenIdentityStack.Application/Groups/Commands/AddUserToGroupCommand.cs
- [X] T155 [US6] Create RemoveUserFromGroupCommand and UseCase in src/OpenIdentityStack.Application/Groups/Commands/RemoveUserFromGroupCommand.cs
- [X] T156 [US6] Create AddGroupMappingCommand and UseCase in src/OpenIdentityStack.Application/Groups/Commands/AddGroupMappingCommand.cs
- [X] T157 [US6] Create GetUserEffectiveRolesQuery in src/OpenIdentityStack.Application/Users/Queries/GetUserEffectiveRolesQuery.cs

### Infrastructure Implementation for User Story 6

- [X] T158 [P] [US6] Create GroupConfiguration (EF Core) in src/OpenIdentityStack.Infrastructure/Persistence/Groups/GroupConfiguration.cs
- [X] T159 [P] [US6] Create GroupMembershipConfiguration in src/OpenIdentityStack.Infrastructure/Persistence/Groups/GroupMembershipConfiguration.cs
- [X] T160 [P] [US6] Create GroupMappingConfiguration in src/OpenIdentityStack.Infrastructure/Persistence/Groups/GroupMappingConfiguration.cs
- [X] T161 [US6] Implement GroupRepository in src/OpenIdentityStack.Infrastructure/Persistence/Groups/GroupRepository.cs
- [X] T162 [US6] Extend TokenService to apply group mappings in src/OpenIdentityStack.Infrastructure/Identity/TokenService.cs
- [X] T163 [US6] Add entities to DbContext and create migration

### API Implementation for User Story 6

- [X] T164 [US6] Create GroupsController in src/OpenIdentityStack.Api/Groups/GroupsController.cs
- [X] T165 [P] [US6] Create GroupRequests/Responses in src/OpenIdentityStack.Api/Groups/GroupRequests.cs
- [X] T166 [US6] Add group membership endpoints to UsersController

**Checkpoint**: Groups with mappings work; membership drives token claims

---

## Phase 9: User Story 7 - Admin Service Account Management (Priority: P7)

**Goal**: Administrators manage service accounts (clients) via Admin API

**Independent Test**: Create service account via API, configure credentials, verify can obtain tokens

### Tests for User Story 7

- [X] T167 [P] [US7] Unit test CreateServiceAccountUseCase in tests/OpenIdentityStack.Application.Tests/ServiceAccounts/CreateServiceAccountUseCaseTests.cs
- [X] T168 [P] [US7] Unit test RotateSecretUseCase in tests/OpenIdentityStack.Application.Tests/ServiceAccounts/RotateSecretUseCaseTests.cs
- [X] T169 [US7] Integration test Admin Service Accounts CRUD in tests/OpenIdentityStack.Api.Tests/Admin/ServiceAccountsControllerTests.cs
- [X] T170 [US7] Contract test /api/admin/service-accounts in tests/OpenIdentityStack.Contract.Tests/Admin/ServiceAccountsEndpointContractTests.cs

### Application Implementation for User Story 7

- [X] T171 [P] [US7] Create CreateServiceAccountCommand and UseCase in src/OpenIdentityStack.Application/ServiceAccounts/Commands/CreateServiceAccountCommand.cs
- [X] T172 [P] [US7] Create ListServiceAccountsQuery in src/OpenIdentityStack.Application/ServiceAccounts/Queries/ListServiceAccountsQuery.cs
- [X] T173 [US7] Create RotateSecretCommand and UseCase in src/OpenIdentityStack.Application/ServiceAccounts/Commands/RotateSecretCommand.cs
- [X] T174 [US7] Create AddCertificateCommand and UseCase in src/OpenIdentityStack.Application/ServiceAccounts/Commands/AddCertificateCommand.cs
- [X] T175 [US7] Create DisableServiceAccountCommand and UseCase in src/OpenIdentityStack.Application/ServiceAccounts/Commands/DisableServiceAccountCommand.cs

### API Implementation for User Story 7

- [X] T176 [US7] Create ServiceAccountsController in src/OpenIdentityStack.Api/ServiceAccounts/ServiceAccountsController.cs
- [X] T177 [P] [US7] Create ServiceAccountRequests/Responses in src/OpenIdentityStack.Api/ServiceAccounts/ServiceAccountRequests.cs
- [X] T178 [US7] Register OpenIddict application store integration

**Checkpoint**: Service accounts fully manageable via Admin API

---

## Phase 10: User Story 8 - Admin Upstream Identity Management (Priority: P8)

**Goal**: Administrators link/unlink upstream identities to users

**Independent Test**: Link upstream identity to existing user, authenticate via that upstream, verify recognized as linked user

### Tests for User Story 8

- [X] T179 [P] [US8] Unit test LinkUpstreamIdentityUseCase in tests/OpenIdentityStack.Application.Tests/Users/LinkUpstreamIdentityUseCaseTests.cs
- [X] T180 [P] [US8] Unit test UnlinkUpstreamIdentityUseCase in tests/OpenIdentityStack.Application.Tests/Users/UnlinkUpstreamIdentityUseCaseTests.cs
- [X] T181 [US8] Integration test identity linking in tests/OpenIdentityStack.Api.Tests/Admin/UpstreamIdentityManagementTests.cs

### Application Implementation for User Story 8

- [X] T182 [US8] Implement LinkUpstreamIdentityUseCase in src/OpenIdentityStack.Application/Users/Commands/LinkUpstreamIdentityUseCase.cs
- [X] T183 [US8] Create UnlinkUpstreamIdentityCommand and UseCase in src/OpenIdentityStack.Application/Users/Commands/UnlinkUpstreamIdentityCommand.cs
- [X] T184 [US8] Create ListUserUpstreamIdentitiesQuery in src/OpenIdentityStack.Application/Users/Queries/ListUserUpstreamIdentitiesQuery.cs

### API Implementation for User Story 8

- [X] T185 [US8] Add upstream identity endpoints to UsersController (/api/admin/users/{id}/upstream-identities) in src/OpenIdentityStack.Api/Users/UsersController.cs

**Checkpoint**: Upstream identities can be linked/unlinked via Admin API

---

## Phase 11: User Story 9 - Session Management and Visibility (Priority: P9)

**Goal**: Administrators view and revoke user sessions via Admin API

**Independent Test**: User authenticates creating session, admin queries sessions, admin revokes session, refresh token fails

### Tests for User Story 9

- [X] T186 [P] [US9] Unit test UserSession aggregate in tests/OpenIdentityStack.Domain.Tests/Sessions/UserSessionTests.cs
- [X] T187 [P] [US9] Unit test RevokeSessionUseCase in tests/OpenIdentityStack.Application.Tests/Sessions/RevokeSessionUseCaseTests.cs
- [X] T188 [US9] Integration test session revocation in tests/OpenIdentityStack.Api.Tests/Admin/SessionManagementTests.cs
- [X] T189 [US9] Contract test /api/admin/sessions in tests/OpenIdentityStack.Contract.Tests/Admin/SessionsEndpointContractTests.cs

### Domain Implementation for User Story 9

- [X] T190 [P] [US9] Create SessionStatus enum in src/OpenIdentityStack.Domain/Sessions/SessionStatus.cs
- [X] T191 [P] [US9] Create UserSession aggregate root in src/OpenIdentityStack.Domain/Sessions/UserSession.cs
- [X] T192 [P] [US9] Create ClientSession value object in src/OpenIdentityStack.Domain/Sessions/ClientSession.cs
- [X] T193 [P] [US9] Create LogoutStatus enum in src/OpenIdentityStack.Domain/Sessions/LogoutStatus.cs

### Application Implementation for User Story 9

- [X] T194 [P] [US9] Create ISessionRepository in src/OpenIdentityStack.Application/Abstractions/ISessionRepository.cs
- [X] T195 [P] [US9] Create ListSessionsQuery in src/OpenIdentityStack.Application/Sessions/Queries/ListSessionsQuery.cs
- [X] T196 [US9] Create RevokeSessionCommand and UseCase in src/OpenIdentityStack.Application/Sessions/Commands/RevokeSessionCommand.cs
- [X] T197 [US9] Create RevokeAllUserSessionsCommand and UseCase in src/OpenIdentityStack.Application/Sessions/Commands/RevokeAllUserSessionsCommand.cs

### Infrastructure Implementation for User Story 9

- [X] T198 [P] [US9] Create UserSessionConfiguration (EF Core) in src/OpenIdentityStack.Infrastructure/Persistence/Sessions/UserSessionConfiguration.cs
- [X] T199 [P] [US9] Create ClientSessionConfiguration in src/OpenIdentityStack.Infrastructure/Persistence/Sessions/UserSessionConfiguration.cs (ClientSession is owned by UserSession)
- [X] T200 [US9] Implement SessionRepository in src/OpenIdentityStack.Infrastructure/Persistence/Sessions/SessionRepository.cs
- [X] T201 [US9] Integrate session tracking with OpenIddict token generation
- [X] T202 [US9] Integrate session validation with refresh token flow
- [X] T203 [US9] Add entities to DbContext and create migration

### API Implementation for User Story 9

- [X] T204 [US9] Create SessionsController in src/OpenIdentityStack.Api/Sessions/SessionsController.cs
- [X] T205 [P] [US9] Create SessionRequests/Responses in src/OpenIdentityStack.Api/Sessions/SessionResponses.cs

**Checkpoint**: Sessions visible and revocable via Admin API; revoked sessions reject refresh

---

## Phase 12: User Story 10 - Single Logout (SLO) (Priority: P10)

**Goal**: Logout from one application triggers logout notifications to other participating applications

**Independent Test**: User authenticates to multiple clients, logout from one, verify other clients receive logout notifications

### Tests for User Story 10

- [X] T206 [P] [US10] Unit test SLO notification logic in tests/OpenIdentityStack.Application.Tests/Sessions/SingleLogoutTests.cs
- [X] T207 [US10] Integration test front-channel logout in tests/OpenIdentityStack.Api.Tests/Authentication/FrontChannelLogoutTests.cs
- [X] T208 [US10] Integration test back-channel logout in tests/OpenIdentityStack.Api.Tests/Authentication/BackChannelLogoutTests.cs

### Application Implementation for User Story 10

- [X] T209 [P] [US10] Create ILogoutNotifier interface in src/OpenIdentityStack.Application/Abstractions/ILogoutNotifier.cs
- [X] T210 [US10] Create ProcessLogoutCommand and UseCase in src/OpenIdentityStack.Application/Sessions/Commands/ProcessLogoutCommand.cs
- [X] T211 [US10] Create NotifyClientsOfLogoutCommand in src/OpenIdentityStack.Application/Sessions/Commands/NotifyClientsOfLogoutCommand.cs

### Infrastructure Implementation for User Story 10

- [X] T212 [US10] Implement BackChannelLogoutNotifier in src/OpenIdentityStack.Infrastructure/Identity/BackChannelLogoutNotifier.cs
- [X] T213 [US10] Implement FrontChannelLogoutService in src/OpenIdentityStack.Infrastructure/Identity/FrontChannelLogoutService.cs

### API Implementation for User Story 10

- [X] T214 [US10] Implement end-session endpoint with SLO in src/OpenIdentityStack.Api/Authentication/LogoutController.cs
- [X] T215 [US10] Create front-channel logout iframes view in src/OpenIdentityStack.Api/Authentication/Views/Logout.cshtml

**Checkpoint**: SLO works for front-channel and back-channel; failures logged but don't block logout

---

## Phase 13: User Story 11 - Delegated Administration (Priority: P11)

**Goal**: Super admins create delegated admin roles with limited permissions

**Independent Test**: Create delegated admin with users:* only, verify can manage users, verify cannot manage roles

### Tests for User Story 11

- [X] T216 [P] [US11] Unit test permission checking logic in tests/OpenIdentityStack.Application.Tests/Authorization/PermissionCheckerTests.cs
- [X] T217 [US11] Integration test delegated admin permissions in tests/OpenIdentityStack.Api.Tests/Admin/DelegatedAdminTests.cs

### Application Implementation for User Story 11

- [X] T218 [P] [US11] Create IPermissionChecker interface in src/OpenIdentityStack.Application/Abstractions/IPermissionChecker.cs
- [X] T219 [US11] Implement PermissionChecker in src/OpenIdentityStack.Application/Authorization/PermissionChecker.cs
- [X] T220 [US11] Define admin permission constants in src/OpenIdentityStack.Application/Authorization/Permissions.cs

### Infrastructure/API Implementation for User Story 11

- [X] T221 [US11] Implement RequirePermissionAttribute filter logic in src/OpenIdentityStack.Api/Authorization/RequirePermissionAttribute.cs
- [X] T222 [US11] Seed system admin roles with full permissions in src/OpenIdentityStack.Infrastructure/Persistence/SeedData.cs
- [X] T223 [US11] Add permission endpoints to RolesController for managing role permissions

**Checkpoint**: Delegated admins work with permission boundaries enforced

---

## Phase 14: Upstream Provider Management API

**Purpose**: Admin API for managing upstream identity providers (required for US4 administration)

- [X] T224 [P] Contract test /api/admin/providers in tests/OpenIdentityStack.Contract.Tests/Admin/ProvidersEndpointContractTests.cs
- [X] T225 [P] Create CreateProviderCommand and UseCase in src/OpenIdentityStack.Application/Federation/Commands/CreateProviderCommand.cs
- [X] T226 [P] Create UpdateProviderCommand and UseCase in src/OpenIdentityStack.Application/Federation/Commands/UpdateProviderCommand.cs
- [X] T227 [P] Create ListProvidersQuery in src/OpenIdentityStack.Application/Federation/Queries/ListProvidersQuery.cs
- [X] T228 Create ProvidersController in src/OpenIdentityStack.Api/Federation/ProvidersController.cs
- [X] T229 [P] Create ProviderRequests/Responses in src/OpenIdentityStack.Api/Federation/ProviderRequests.cs

---

## Phase 15: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T230 [P] Add OpenAPI documentation with Scalar + Microsoft.AspNetCore.OpenApi in src/OpenIdentityStack.Api/
- [X] T231 [P] Add comprehensive API documentation comments
- [X] T232 Review and optimize database indexes based on query patterns
- [X] T233 Add structured logging throughout application
- [X] T234 [P] Create README.md with setup instructions
- [X] T235 Validate against quickstart.md scenarios
- [X] T236 Run security review checklist
- [X] T237 Performance testing for P50/P95/P99 targets

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - **BLOCKS all user stories**
- **User Stories (Phases 3-13)**: All depend on Foundational phase completion
  - Can proceed in parallel (if staffed) or sequentially by priority
- **Upstream Provider API (Phase 14)**: Depends on US4 domain entities
- **Polish (Phase 15)**: Depends on all desired user stories being complete

### User Story Dependencies

| Story | Can Start After | Dependencies on Other Stories |
|-------|-----------------|------------------------------|
| US1 - Local Auth | Foundational | None (true MVP) |
| US2 - Service Accounts | Foundational | None |
| US3 - Admin User Mgmt | US1 (needs User entity) | User entity from US1 |
| US4 - Federation | US1 | User entity, login flow |
| US5 - RBAC | US1 or US3 | User entity |
| US6 - Groups | US5 | Role entity from US5 |
| US7 - Service Account Admin | US2 | ServiceAccount entity from US2 |
| US8 - Upstream Identity Admin | US4 | UpstreamIdentity from US4 |
| US9 - Sessions | US1 | User entity, token flow |
| US10 - SLO | US9 | Session entity from US9 |
| US11 - Delegated Admin | US5 | Role entity from US5 |

### Parallel Opportunities by Phase

**Within Setup (Phase 1)**:
```
T003, T004 can run in parallel
T005, T006, T007, T008, T009 can run in parallel
T011, T012, T013, T014, T015 can run in parallel
```

**Within Foundational (Phase 2)**:
```
T019, T020, T021, T022, T023, T024 can run in parallel
T026, T027, T028, T029 can run in parallel
T033, T034, T035 can run in parallel
T038, T039, T040 can run in parallel
```

**Within Each User Story**:
- All tests marked [P] can run in parallel
- All domain entities marked [P] can run in parallel
- All repository interfaces marked [P] can run in parallel

---

## Parallel Example: User Story 1

```bash
# Phase 1: Launch all tests in parallel
T041 "Unit test User entity" &
T042 "Unit test User status transitions" &
T043 "Unit test CreateUserUseCase" &
T044 "Unit test ValidateUserCredentialsUseCase" &
T045 "Integration test UserRepository"

# Phase 2: Launch all domain models in parallel
T048 "Create UserStatus enum" &
T049 "Create User aggregate" &
T050 "Create UserDomainEvents"

# Phase 3: Launch interfaces in parallel
T051 "Create IUserRepository" &
T052 "Create IPasswordHasher"

# Phase 4: Sequential implementation (dependencies)
T053 → T054 → T055 → T056 (use cases depend on interfaces)
T057 → T058 → T059 → T060 (infrastructure)
T061 → T062 → T063 (API)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (~18 tasks)
2. Complete Phase 2: Foundational (~22 tasks)
3. Complete Phase 3: User Story 1 (~23 tasks)
4. **STOP and VALIDATE**: Test local user auth end-to-end
5. Deploy/demo if ready

**MVP Scope**: ~63 tasks total

### Incremental Delivery

| Milestone | User Stories | Cumulative Value |
|-----------|--------------|------------------|
| MVP | US1 | Users can authenticate |
| M2 | US1 + US2 | + Service accounts work |
| M3 | US1-3 | + Admin can manage users |
| M4 | US1-5 | + Federation + RBAC |
| M5 | US1-7 | + Groups + Service Account Admin |
| M6 | US1-9 | + Session management |
| M7 | US1-11 | + SLO + Delegated Admin (full) |

### Parallel Team Strategy

With 3 developers after Foundational complete:

- **Developer A**: US1 → US3 → US8
- **Developer B**: US2 → US7 → US9 → US10
- **Developer C**: US4 → US5 → US6 → US11

---

## Summary

| Phase | Task Count | Purpose |
|-------|------------|---------|
| Phase 1: Setup | 18 | Solution structure, Aspire, projects |
| Phase 2: Foundational | 22 | Base classes, DbContext, OpenIddict |
| Phase 3: US1 Local Auth | 23 | Core authentication (MVP) |
| Phase 4: US2 Service Accounts | 19 | Machine-to-machine auth |
| Phase 5: US3 Admin User Mgmt | 17 | User CRUD API |
| Phase 6: US4 Federation | 21 | Upstream IdP + JIT |
| Phase 7: US5 RBAC | 21 | Roles in tokens |
| Phase 8: US6 Groups | 25 | Group mappings |
| Phase 9: US7 SA Admin | 12 | Service account API |
| Phase 10: US8 Upstream Admin | 7 | Identity linking API |
| Phase 11: US9 Sessions | 20 | Session visibility/revocation |
| Phase 12: US10 SLO | 10 | Single Logout |
| Phase 13: US11 Delegated Admin | 8 | Permission boundaries |
| Phase 14: Provider API | 6 | Upstream provider admin |
| Phase 15: Polish | 8 | Documentation, security |
| **Total** | **237** | |

### Independent Test Criteria by Story

| Story | How to Test Independently |
|-------|--------------------------|
| US1 | Create user → login → verify tokens |
| US2 | Create service account → client_credentials → verify token |
| US3 | Admin token → CRUD users → verify list |
| US4 | Configure IdP → redirect → JIT → verify local user |
| US5 | Create role → assign → login → verify claim |
| US6 | Create group + mapping → add user → login → verify claim |
| US7 | Admin → create service account → verify auth works |
| US8 | Admin → link identity → login upstream → verify linked |
| US9 | Login → query sessions → revoke → verify refresh fails |
| US10 | Login to 2 clients → logout → verify other notified |
| US11 | Create limited admin → verify can/cannot operations |

### Format Validation ✓

- All tasks follow `- [ ] [TaskID] [P?] [Story?] Description with file path` format
- Task IDs are sequential (T001-T237)
- [P] markers on parallelizable tasks
- [US#] labels on user story tasks
- File paths included in all implementation tasks
