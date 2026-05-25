# Tasks: Unify Applications Domain

**Input**: Design documents from `/specs/006-unify-applications-model/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/applications.openapi.yaml, quickstart.md

**Tests**: Required for this behavior-changing IAM feature. Test tasks appear before implementation tasks in each user story and should fail before implementation.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it touches different files and has no dependency on an incomplete task in the same phase
- **[Story]**: User story label (`US1`, `US2`, `US3`) used only inside user-story phases
- Every task includes an exact repository path

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the shared feature skeleton and non-behavioral contracts needed before implementation.

- [X] T001 Create backend Applications directory skeletons in src/OpenIdentityStack.Domain/Applications/, src/OpenIdentityStack.Application/Applications/, src/OpenIdentityStack.Infrastructure/Applications/, src/OpenIdentityStack.Infrastructure/Persistence/Applications/, and src/OpenIdentityStack.Api/Applications/
- [X] T002 [P] Create backend test directory skeletons in tests/OpenIdentityStack.Domain.Tests/Applications/, tests/OpenIdentityStack.Application.Tests/Applications/, tests/OpenIdentityStack.Infrastructure.Tests/Applications/, tests/OpenIdentityStack.Api.Tests/Admin/Applications/, and tests/OpenIdentityStack.Contract.Tests/Admin/Applications/
- [X] T003 [P] Create AdminWeb Applications feature skeleton in src/OpenIdentityStack.AdminWeb/src/features/applications/api/, src/OpenIdentityStack.AdminWeb/src/features/applications/hooks/, src/OpenIdentityStack.AdminWeb/src/features/applications/components/, and src/OpenIdentityStack.AdminWeb/src/features/applications/pages/
- [X] T004 [P] Add generated API contract fixture for implementation reference in tests/OpenIdentityStack.Contract.Tests/Admin/Applications/applications.openapi.yaml from specs/006-unify-applications-model/contracts/applications.openapi.yaml
- [X] T005 [P] Add validation command checklist notes in specs/006-unify-applications-model/quickstart.md for domain, application, infrastructure, API, contract, AdminWeb, docs, and Aspire validation

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish shared domain/application/security/persistence contracts that all user stories depend on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T006 [P] Add `ApplicationId` strongly typed ID in src/OpenIdentityStack.Domain/Applications/ApplicationId.cs and converter coverage in tests/OpenIdentityStack.Domain.Tests/Applications/ApplicationIdTests.cs
- [X] T007 [P] Add application enums in src/OpenIdentityStack.Domain/Applications/ApplicationType.cs, src/OpenIdentityStack.Domain/Applications/OAuthClientType.cs, src/OpenIdentityStack.Domain/Applications/ApplicationStatus.cs, src/OpenIdentityStack.Domain/Applications/ApplicationCredentialType.cs, and src/OpenIdentityStack.Domain/Applications/OAuthGrantType.cs
- [X] T008 [P] Add application domain errors in src/OpenIdentityStack.Domain/Applications/ApplicationErrors.cs
- [X] T009 [P] Add application domain events in src/OpenIdentityStack.Domain/Applications/ApplicationDomainEvents.cs
- [X] T010 [P] Define `IApplicationRepository` in src/OpenIdentityStack.Application/Applications/IApplicationRepository.cs
- [X] T011 [P] Define `IApplicationProtocolProjection` in src/OpenIdentityStack.Application/Abstractions/IApplicationProtocolProjection.cs
- [X] T012 [P] Add unified application permissions in src/OpenIdentityStack.Application/Authorization/Permissions.cs with `applications:read`, `applications:write`, `applications:delete`, `applications:manage-credentials`, `applications:manage-certificates`, and `applications:*`
- [X] T013 [P] Add permission mapping tests in tests/OpenIdentityStack.Application.Tests/Authorization/ApplicationPermissionMappingTests.cs
- [X] T014 [P] Add application query DTOs in src/OpenIdentityStack.Application/Applications/Queries/ApplicationDetails.cs, src/OpenIdentityStack.Application/Applications/Queries/ApplicationSummary.cs, and src/OpenIdentityStack.Application/Applications/Queries/ApplicationCredentialDetails.cs
- [X] T015 [P] Add application command result DTOs in src/OpenIdentityStack.Application/Applications/Commands/ApplicationCommandResults.cs
- [X] T016 Add `DbSet<Application>` and `DbSet<ApplicationCredential>` declarations to src/OpenIdentityStack.Infrastructure/Persistence/OpenIdentityStackDbContext.cs
- [X] T017 [P] Add application EF configuration shell in src/OpenIdentityStack.Infrastructure/Persistence/Applications/ApplicationConfiguration.cs
- [X] T018 [P] Add application credential EF configuration shell in src/OpenIdentityStack.Infrastructure/Persistence/Applications/ApplicationCredentialConfiguration.cs
- [X] T019 Register application repository, use cases, query handlers, protocol projection, and authentication handler in src/OpenIdentityStack.Infrastructure/ServiceCollectionExtensions.cs
- [X] T020 Register Applications API route group in src/OpenIdentityStack.Api/Program.cs

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Manage one application model (Priority: P1) MVP

**Goal**: Administrators can create, list, view, update, disable, enable, and delete applications through the unified domain, persistence, API, and AdminWeb model without using legacy client/service-account concepts.

**Independent Test**: Create, view, update, disable/enable, and delete an application through `/api/admin/applications` and AdminWeb Applications screens; verify the application uses the unified model and does not require legacy APIs.

### Tests for User Story 1

- [X] T021 [P] [US1] Add domain tests for application creation, client ID validation, display name validation, metadata updates, enable/disable, and delete behavior in tests/OpenIdentityStack.Domain.Tests/Applications/ApplicationTests.cs
- [X] T022 [P] [US1] Add domain tests for grant/client-type/redirect/PKCE/profile invariants in tests/OpenIdentityStack.Domain.Tests/Applications/ApplicationOAuthConfigurationTests.cs
- [X] T023 [P] [US1] Add application use-case tests for create/update/configure/enable/disable/delete in tests/OpenIdentityStack.Application.Tests/Applications/ApplicationLifecycleUseCaseTests.cs
- [X] T024 [P] [US1] Add query handler tests for get/list application behavior and filters in tests/OpenIdentityStack.Application.Tests/Applications/ApplicationQueryHandlerTests.cs
- [X] T025 [P] [US1] Add repository tests for application persistence, unique client ID lookup, paging, filters, and status changes in tests/OpenIdentityStack.Infrastructure.Tests/Applications/ApplicationRepositoryTests.cs
- [X] T026 [P] [US1] Add API workflow tests for create/get/list/update/configure/disable/enable/delete endpoints in tests/OpenIdentityStack.Api.Tests/Admin/Applications/ApplicationsEndpointWorkflowTests.cs
- [X] T027 [P] [US1] Add contract tests for `/api/admin/applications` create/get/list/update/configure/status/delete shape in tests/OpenIdentityStack.Contract.Tests/Admin/Applications/ApplicationsEndpointContractTests.cs
- [X] T028 [P] [US1] Add AdminWeb API client tests for applications list/get/create/update/delete calls in src/OpenIdentityStack.AdminWeb/src/features/applications/api/applications-api.test.ts
- [X] T029 [P] [US1] Add AdminWeb component tests for application list, form, and detail baseline behavior in src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationManagement.test.tsx

### Implementation for User Story 1

- [X] T030 [P] [US1] Implement `Application` aggregate lifecycle and OAuth configuration methods in src/OpenIdentityStack.Domain/Applications/Application.cs
- [X] T031 [P] [US1] Implement application domain event raising for create/update/configure/enable/disable/delete in src/OpenIdentityStack.Domain/Applications/ApplicationDomainEvents.cs
- [X] T032 [P] [US1] Implement create/update/configure command records and interfaces in src/OpenIdentityStack.Application/Applications/Commands/CreateApplicationCommand.cs, src/OpenIdentityStack.Application/Applications/Commands/UpdateApplicationMetadataCommand.cs, and src/OpenIdentityStack.Application/Applications/Commands/ConfigureApplicationOAuthCommand.cs
- [X] T033 [P] [US1] Implement lifecycle command records and interfaces in src/OpenIdentityStack.Application/Applications/Commands/EnableApplicationCommand.cs, src/OpenIdentityStack.Application/Applications/Commands/DisableApplicationCommand.cs, and src/OpenIdentityStack.Application/Applications/Commands/DeleteApplicationCommand.cs
- [X] T034 [P] [US1] Implement query contracts in src/OpenIdentityStack.Application/Applications/Queries/GetApplicationQuery.cs, src/OpenIdentityStack.Application/Applications/Queries/ListApplicationsQuery.cs, src/OpenIdentityStack.Application/Applications/Queries/IGetApplicationQueryHandler.cs, and src/OpenIdentityStack.Application/Applications/Queries/IListApplicationsQueryHandler.cs
- [X] T035 [US1] Implement create/update/configure/enable/disable/delete use cases in src/OpenIdentityStack.Infrastructure/Applications/ApplicationLifecycleUseCases.cs
- [X] T036 [US1] Implement get/list query handlers in src/OpenIdentityStack.Infrastructure/Applications/ApplicationQueryHandlers.cs
- [X] T037 [US1] Implement `ApplicationRepository` with client ID uniqueness, paging, type/status/clientType/search filters, and save/delete behavior in src/OpenIdentityStack.Infrastructure/Applications/ApplicationRepository.cs
- [X] T038 [US1] Complete EF mapping for Application fields, JSON collections, indexes, and constraints in src/OpenIdentityStack.Infrastructure/Persistence/Applications/ApplicationConfiguration.cs
- [X] T039 [US1] Complete EF mapping for ApplicationCredential relationship shell without credential behavior in src/OpenIdentityStack.Infrastructure/Persistence/Applications/ApplicationCredentialConfiguration.cs
- [X] T040 [US1] Add migration for `Applications` and `ApplicationCredentials` table creation in src/OpenIdentityStack.Infrastructure/Persistence/Migrations/
- [X] T041 [US1] Implement OpenIddict projection upsert/delete/disable mapping for application metadata, grants, scopes, redirects, PKCE, consent, and status in src/OpenIdentityStack.Infrastructure/Identity/OpenIddictApplicationProjection.cs
- [X] T042 [US1] Replace client-specific projection dependency in client creation flow with `IApplicationProtocolProjection` where needed in src/OpenIdentityStack.Infrastructure/Clients/CreateClientUseCase.cs
- [X] T043 [US1] Implement application request/response DTOs and explicit mapping in src/OpenIdentityStack.Api/Applications/ApplicationRequests.cs
- [X] T044 [US1] Implement `/api/admin/applications` create/get/list/patch/oauth/disable/enable/delete endpoints with authorization and safe error responses in src/OpenIdentityStack.Api/Applications/ApplicationsApi.cs
- [X] T045 [US1] Add Scalar/OpenAPI summaries, response types, and permission metadata for Applications endpoints in src/OpenIdentityStack.Api/Applications/ApplicationsApi.cs
- [X] T046 [US1] Implement AdminWeb applications API module in src/OpenIdentityStack.AdminWeb/src/features/applications/api/applications-api.ts
- [X] T047 [P] [US1] Implement AdminWeb application query/mutation hooks in src/OpenIdentityStack.AdminWeb/src/features/applications/hooks/useApplications.ts, src/OpenIdentityStack.AdminWeb/src/features/applications/hooks/useApplication.ts, src/OpenIdentityStack.AdminWeb/src/features/applications/hooks/useCreateApplication.ts, src/OpenIdentityStack.AdminWeb/src/features/applications/hooks/useUpdateApplication.ts, and src/OpenIdentityStack.AdminWeb/src/features/applications/hooks/useDeleteApplication.ts
- [X] T048 [P] [US1] Implement AdminWeb application list and status/type badge components in src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationList.tsx and src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationStatusBadge.tsx
- [X] T049 [P] [US1] Implement AdminWeb application form and detail baseline components in src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationForm.tsx and src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationDetail.tsx
- [X] T050 [US1] Implement AdminWeb Applications pages and exports in src/OpenIdentityStack.AdminWeb/src/features/applications/pages/ApplicationsPage.tsx, src/OpenIdentityStack.AdminWeb/src/features/applications/pages/CreateApplicationPage.tsx, src/OpenIdentityStack.AdminWeb/src/features/applications/pages/ApplicationDetailPage.tsx, src/OpenIdentityStack.AdminWeb/src/features/applications/pages/EditApplicationPage.tsx, src/OpenIdentityStack.AdminWeb/src/features/applications/pages/index.ts, and src/OpenIdentityStack.AdminWeb/src/features/applications/index.ts
- [X] T051 [US1] Replace Clients and Service Accounts navigation entries with Applications in src/OpenIdentityStack.AdminWeb/src/components/layout/Sidebar.tsx
- [X] T052 [US1] Add Applications routes in src/OpenIdentityStack.AdminWeb/src/routes/index.tsx
- [X] T053 [US1] Add application lifecycle audit event emission in src/OpenIdentityStack.Infrastructure/Audit/AuditLogService.cs

**Checkpoint**: User Story 1 is independently functional when unified application CRUD, status transitions, OpenIddict projection, API contract tests, and baseline AdminWeb flows pass without legacy endpoints.

---

## Phase 4: User Story 2 - Manage machine-to-machine applications safely (Priority: P2)

**Goal**: Security administrators can configure machine-to-machine applications with safe defaults and manage secrets/certificates with one-time secret display, revocation, expiration, and token validation.

**Independent Test**: Create a machine-to-machine application, add/rotate/revoke secrets and certificates, reject invalid grants/redirects/public credentials, and verify token issuance accepts only active credentials for active confidential applications.

### Tests for User Story 2

- [X] T054 [P] [US2] Add domain tests for ApplicationCredential secret/certificate creation, revocation, expiration, active state, and public-application rejection in tests/OpenIdentityStack.Domain.Tests/Applications/ApplicationCredentialTests.cs
- [X] T055 [P] [US2] Add domain tests for machine-to-machine safe defaults and invalid grant/redirect rejection in tests/OpenIdentityStack.Domain.Tests/Applications/MachineToMachineApplicationTests.cs
- [X] T056 [P] [US2] Add application tests for add secret, revoke credential, add certificate, validate secret, and validate certificate use cases in tests/OpenIdentityStack.Application.Tests/Applications/ApplicationCredentialUseCaseTests.cs
- [X] T057 [P] [US2] Add infrastructure identity tests for application client authentication handler secret/certificate/disabled/revoked/expired behavior in tests/OpenIdentityStack.Infrastructure.Tests/Identity/ApplicationClientAuthenticationHandlerTests.cs
- [X] T058 [P] [US2] Add API tests for secret creation, rotation with revokeExisting, certificate add, credential list, credential revoke, and public credential rejection in tests/OpenIdentityStack.Api.Tests/Admin/Applications/ApplicationCredentialsEndpointWorkflowTests.cs
- [X] T059 [P] [US2] Add token integration tests for machine-to-machine secret success, revoked secret failure, disabled application failure, and certificate validation in tests/OpenIdentityStack.Api.Tests/Authentication/ApplicationClientCredentialsFlowTests.cs
- [X] T060 [P] [US2] Add contract tests for application credential endpoints and one-time secret response shape in tests/OpenIdentityStack.Contract.Tests/Admin/Applications/ApplicationCredentialsContractTests.cs
- [X] T061 [P] [US2] Add AdminWeb tests for application credential dialogs, one-time secret display, and public credential rejection messages in src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationCredentials.test.tsx

### Implementation for User Story 2

- [X] T062 [P] [US2] Implement `ApplicationCredential` entity with secret/certificate metadata and lifecycle in src/OpenIdentityStack.Domain/Applications/ApplicationCredential.cs
- [X] T063 [US2] Add credential methods to `Application` for add secret, add certificate, revoke credential, active credential selection, and LastUsedAt update in src/OpenIdentityStack.Domain/Applications/Application.cs
- [X] T064 [P] [US2] Add credential command records and interfaces in src/OpenIdentityStack.Application/Applications/Commands/AddApplicationSecretCommand.cs, src/OpenIdentityStack.Application/Applications/Commands/AddApplicationCertificateCommand.cs, and src/OpenIdentityStack.Application/Applications/Commands/RevokeApplicationCredentialCommand.cs
- [X] T065 [P] [US2] Add credential validation command records and interfaces in src/OpenIdentityStack.Application/Applications/Commands/ValidateApplicationClientCredentialsCommand.cs and src/OpenIdentityStack.Application/Applications/Commands/ValidateApplicationCertificateCommand.cs
- [X] T066 [P] [US2] Add credential query contracts in src/OpenIdentityStack.Application/Applications/Queries/ListApplicationCredentialsQuery.cs and src/OpenIdentityStack.Application/Applications/Queries/IListApplicationCredentialsQueryHandler.cs
- [X] T067 [US2] Implement secret generation, hashing, one-time return, certificate add, revoke, and credential list use cases in src/OpenIdentityStack.Infrastructure/Applications/ApplicationCredentialUseCases.cs
- [X] T068 [US2] Implement application client secret and certificate validation use cases with disabled/revoked/expired rejection and LastUsedAt update in src/OpenIdentityStack.Infrastructure/Applications/ApplicationCredentialValidationUseCases.cs
- [X] T069 [US2] Complete ApplicationCredential EF mapping for hashes, thumbprints, subjects, expiration, LastUsedAt, RevokedAt, indexes, and parent relationship in src/OpenIdentityStack.Infrastructure/Persistence/Applications/ApplicationCredentialConfiguration.cs
- [X] T070 [US2] Implement `ApplicationClientAuthenticationHandler` to replace service-account-only token validation in src/OpenIdentityStack.Infrastructure/Identity/ApplicationClientAuthenticationHandler.cs
- [X] T071 [US2] Replace OpenIddict service-account validation registration with application validation registration in src/OpenIdentityStack.Infrastructure/Identity/ServiceAccountValidationHandler.cs and src/OpenIdentityStack.Infrastructure/Identity/OpenIddictSetup.cs
- [X] T072 [US2] Remove or obsolete service-account credential validation dependencies from src/OpenIdentityStack.Application/ServiceAccounts/Commands/ValidateClientCredentialsUseCase.cs and src/OpenIdentityStack.Application/ServiceAccounts/Commands/ValidateCertificateUseCase.cs
- [X] T073 [US2] Add credential endpoint DTOs and mapping to src/OpenIdentityStack.Api/Applications/ApplicationRequests.cs
- [X] T074 [US2] Add credential list/add secret/add certificate/revoke endpoints with `applications:manage-credentials` and `applications:manage-certificates` authorization in src/OpenIdentityStack.Api/Applications/ApplicationsApi.cs
- [X] T075 [US2] Add application credential audit events for added, revoked, and used credentials in src/OpenIdentityStack.Infrastructure/Audit/AuditLogService.cs
- [X] T076 [US2] Implement AdminWeb credential API functions in src/OpenIdentityStack.AdminWeb/src/features/applications/api/applications-api.ts
- [X] T077 [P] [US2] Implement AdminWeb credential hooks in src/OpenIdentityStack.AdminWeb/src/features/applications/hooks/useApplicationCredentials.ts, src/OpenIdentityStack.AdminWeb/src/features/applications/hooks/useAddApplicationSecret.ts, src/OpenIdentityStack.AdminWeb/src/features/applications/hooks/useAddApplicationCertificate.ts, and src/OpenIdentityStack.AdminWeb/src/features/applications/hooks/useRevokeApplicationCredential.ts
- [X] T078 [P] [US2] Implement AdminWeb credential management components in src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationCredentials.tsx, src/OpenIdentityStack.AdminWeb/src/features/applications/components/AddApplicationSecretDialog.tsx, src/OpenIdentityStack.AdminWeb/src/features/applications/components/AddApplicationCertificateDialog.tsx, and src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationSecretDisplay.tsx
- [X] T079 [US2] Add credential and OAuth configuration tabs to the application detail page in src/OpenIdentityStack.AdminWeb/src/features/applications/pages/ApplicationDetailPage.tsx

**Checkpoint**: User Story 2 is independently functional when machine-to-machine validation, credential lifecycle, token endpoint behavior, and AdminWeb credential workflows pass without leaking plain secrets or hashes.

---

## Phase 5: User Story 3 - Migrate existing registrations with continuity (Priority: P3)

**Goal**: Platform operators can migrate existing clients, service accounts, credentials, certificates, permissions, and admin consumers into the unified application model while preserving stable client identifiers and failing safely on conflicts.

**Independent Test**: Run migration preflight/backfill against representative legacy data, verify client ID preservation and permission mapping, and confirm duplicate client IDs or invalid grants fail before mutation.

### Tests for User Story 3

- [X] T080 [P] [US3] Add migration preflight tests for duplicate client IDs, invalid service-account grants, ambiguous client profile review, and no-mutation failure behavior in tests/OpenIdentityStack.Infrastructure.Tests/Persistence/Applications/ApplicationMigrationPreflightTests.cs
- [X] T081 [P] [US3] Add migration backfill tests for Clients to Applications mapping and client ID preservation in tests/OpenIdentityStack.Infrastructure.Tests/Persistence/Applications/ClientToApplicationMigrationTests.cs
- [X] T082 [P] [US3] Add migration backfill tests for ServiceAccounts, ClientCredentials, and ClientCertificates to Applications/ApplicationCredentials mapping in tests/OpenIdentityStack.Infrastructure.Tests/Persistence/Applications/ServiceAccountToApplicationMigrationTests.cs
- [X] T083 [P] [US3] Add permission migration tests for legacy client/service-account permissions to application permissions in tests/OpenIdentityStack.Infrastructure.Tests/Persistence/Applications/ApplicationPermissionMigrationTests.cs
- [X] T084 [P] [US3] SUPERSEDED by T117: earlier compatibility tests for `/api/admin/clients` adapters/deprecation metadata in tests/OpenIdentityStack.Api.Tests/Admin/Applications/ClientsCompatibilityEndpointTests.cs
- [X] T085 [P] [US3] SUPERSEDED by T117: earlier compatibility tests for `/api/admin/service-accounts` adapters/deprecation metadata in tests/OpenIdentityStack.Api.Tests/Admin/Applications/ServiceAccountsCompatibilityEndpointTests.cs
- [X] T086 [P] [US3] SUPERSEDED by T118: earlier contract tests for deprecated compatibility response headers and replacement metadata in tests/OpenIdentityStack.Contract.Tests/Admin/Applications/ApplicationsCompatibilityContractTests.cs
- [X] T087 [P] [US3] Add AdminWeb tests verifying Applications navigation replaces legacy Clients and Service Accounts navigation labels in src/OpenIdentityStack.AdminWeb/src/features/applications/pages/ApplicationsNavigation.test.tsx

### Implementation for User Story 3

- [X] T088 [US3] Implement migration preflight service for duplicate client IDs, invalid service-account grants, and ambiguous client profiles in src/OpenIdentityStack.Infrastructure/Persistence/Applications/ApplicationMigrationPreflight.cs
- [X] T089 [US3] Add EF migration for Applications backfill from `Clients` with type inference and `RequiresMigrationReview` flags in src/OpenIdentityStack.Infrastructure/Persistence/Migrations/
- [X] T090 [US3] Add EF migration for Applications backfill from `ServiceAccounts` with strict production failure on non-`client_credentials` grants in src/OpenIdentityStack.Infrastructure/Persistence/Migrations/
- [X] T091 [US3] Add EF migration for ApplicationCredentials backfill from `ClientCredentials` and `ClientCertificates` in src/OpenIdentityStack.Infrastructure/Persistence/Migrations/
- [X] T092 [US3] Add EF migration for role permission mapping from `clients:*` and `service-accounts:*` to `applications:*` permissions in src/OpenIdentityStack.Infrastructure/Persistence/Migrations/
- [X] T093 [US3] Update seed data to include unified application permissions and preserve admin access in src/OpenIdentityStack.Infrastructure/Persistence/SeedData.cs
- [X] T094 [US3] Update `Permissions.GetAllPermissions()` ordering and deprecate old permission constants after mapping in src/OpenIdentityStack.Application/Authorization/Permissions.cs
- [X] T095 [US3] SUPERSEDED by T119: earlier Clients API compatibility adapter in src/OpenIdentityStack.Api/Clients/ClientsApi.cs
- [X] T096 [US3] SUPERSEDED by T120: earlier Service Accounts API compatibility adapter in src/OpenIdentityStack.Api/ServiceAccounts/ServiceAccountsApi.cs
- [X] T097 [US3] SUPERSEDED by T121: earlier compatibility enable/disable configuration option binding in src/OpenIdentityStack.Api/Options/ApplicationCompatibilityOptions.cs
- [X] T098 [US3] SUPERSEDED by T121/T127: earlier compatibility configuration and rollback notes in deploy/appsettings.Production.template.json
- [X] T099 [US3] SUPERSEDED by T126: earlier legacy AdminWeb clients and service-account route exports/redirects in src/OpenIdentityStack.AdminWeb/src/features/clients/index.ts and src/OpenIdentityStack.AdminWeb/src/features/service-accounts/index.ts
- [X] T100 [US3] SUPERSEDED by T126: earlier AdminWeb legacy client/service-account redirects in src/OpenIdentityStack.AdminWeb/src/routes/index.tsx
- [X] T101 [US3] SUPERSEDED by T127: earlier docs for compatibility window and deprecated endpoint replacement in docs/applications-migration.md

**Checkpoint**: User Story 3 is independently functional when legacy data migrates safely, client identifiers are preserved, permissions are mapped, old endpoint compatibility work is removed by Phase 7, and migration failures leave data unchanged.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final hardening, docs, validation, and cleanup across all stories.

- [X] T102 [P] Update administrator terminology docs for Application, Machine-to-machine application, Client ID, and deprecated Service Account wording in docs/admin-applications.md
- [X] T103 [P] Update README feature overview and API examples for unified Applications in README.md
- [X] T104 [P] SUPERSEDED by T127 for compatibility flag removal: update deployment guidance for migration ordering, compatibility flags, and rollback in deploy/README.md
- [X] T105 [P] Add AdminWeb screenshot capture or screenshot checklist for changed Applications flows in docs/admin-applications.md
- [X] T106 [P] SUPERSEDED by T119/T120: earlier OpenAPI/Scalar deprecation descriptions for legacy endpoints in src/OpenIdentityStack.Api/Clients/ClientsApi.cs and src/OpenIdentityStack.Api/ServiceAccounts/ServiceAccountsApi.cs
- [X] T107 [P] Add performance/latency regression coverage for token validation lookup behavior in tests/OpenIdentityStack.Api.Tests/Performance/AuthEndpointLatencyTests.cs
- [X] T108 [P] Add audit log assertions for application lifecycle and credential lifecycle events in tests/OpenIdentityStack.Infrastructure.Tests/Audit/AuditLogServiceTests.cs
- [X] T109 Remove obsolete direct references to service-account-specific credential validation from src/OpenIdentityStack.Infrastructure/Identity/ServiceAccountValidationHandler.cs after application validation replacement is proven
- [X] T110 Remove obsolete OpenIddict service-account registrar usage from src/OpenIdentityStack.Infrastructure/Identity/OpenIddictClientApplicationRegistrar.cs after application projection replacement is proven
- [X] T111 Run restore/build validation documented in specs/006-unify-applications-model/quickstart.md using `dotnet restore OpenIdentityStack.slnx` and `dotnet build OpenIdentityStack.slnx --no-restore`
- [X] T112 Run focused backend tests documented in specs/006-unify-applications-model/quickstart.md for Domain, Application, and Infrastructure test projects
- [X] T113 Run API and contract module tests documented in specs/006-unify-applications-model/quickstart.md for `*Api.Tests.dll` and `*Contract.Tests.dll`
- [ ] T114 Run AdminWeb build/lint/unit validation documented in specs/006-unify-applications-model/quickstart.md from src/OpenIdentityStack.AdminWeb/
- [X] T115 Run AdminWeb E2E validation documented in specs/006-unify-applications-model/quickstart.md for tests/OpenIdentityStack.AdminWeb.E2ETests/OpenIdentityStack.AdminWeb.E2ETests.csproj
- [X] T116 Run docs validation documented in specs/006-unify-applications-model/quickstart.md using `python -m mkdocs build --strict`

---

## Phase 7: Breaking-Change Alignment After Compatibility Decision

**Purpose**: Align already-created code with the resolved decision that OpenIdentityStack is pre-1.0 and does not need legacy admin API compatibility.

- [X] T117 [P] [BREAKING] Update API tests so `/api/admin/clients` and `/api/admin/service-accounts` return `404 Not Found` instead of compatibility responses in tests/OpenIdentityStack.Api.Tests/Admin/Applications/
- [X] T118 [P] [BREAKING] Remove deprecated compatibility contract tests and any deprecation-header expectations from tests/OpenIdentityStack.Contract.Tests/Admin/Applications/
- [X] T119 [BREAKING] Remove legacy Clients API route mapping and compatibility implementation from src/OpenIdentityStack.Api/Program.cs and src/OpenIdentityStack.Api/Clients/ClientsApi.cs
- [X] T120 [BREAKING] Remove legacy Service Accounts API route mapping and compatibility implementation from src/OpenIdentityStack.Api/Program.cs and src/OpenIdentityStack.Api/ServiceAccounts/ServiceAccountsApi.cs
- [X] T121 [BREAKING] Delete `ApplicationCompatibilityOptions` and remove `Applications:Compatibility` option binding/config from src/OpenIdentityStack.Api/Program.cs, deploy/appsettings.Production.template.json, and related tests
- [X] T122 [BREAKING] Update migrations/backfill so new `Application.Id` values are generated instead of preserving old `Client.Id` or `ServiceAccount.Id` values in src/OpenIdentityStack.Infrastructure/Persistence/Migrations/
- [X] T123 [BREAKING] Remove invalid service-account grant compatibility/preflight behavior; unsupported legacy ServiceAccounts data is not preserved and old service-account tables are dropped after Applications become authoritative
- [X] T124 [BREAKING] Add EF cleanup migration that drops legacy `Clients`, `ServiceAccounts`, `ClientCredentials`, and `ClientCertificates` tables after required application data migration
- [X] T125 [BREAKING] Remove legacy permission constants or confine them to migration-only code after `applications:*` role permission mapping is complete
- [X] T126 [BREAKING] Remove legacy AdminWeb `/clients` and `/service-accounts` redirects/exports if they only exist for compatibility in src/OpenIdentityStack.AdminWeb/src/routes/index.tsx and feature index files
- [X] T127 [P] [BREAKING] Update docs, README, deploy guidance, and quickstart to describe removed legacy endpoints instead of compatibility windows or deprecation headers
- [X] T128 [BREAKING] Re-run restore/build plus affected API, contract, infrastructure migration, AdminWeb navigation, and docs validation after compatibility removal

**Checkpoint**: The feature is aligned with the resolved pre-1.0 breaking-change policy when legacy admin API routes are gone, compatibility configuration is gone, old tables are dropped after migration, and docs/tests no longer describe compatibility behavior.

---

## Phase 8: Application Type Policy and Option Railroading

**Purpose**: Model the application type option matrix as API-owned business rules and AdminWeb guardrails. The API must enforce policy even when callers bypass the UI; AdminWeb should railroad administrators into valid choices by hiding unavailable options and showing fixed defaults.

### Tests for Application Type Policy

- [x] T129 [P] [POLICY] Add domain tests for Web, Single Page, Native, Machine-to-machine, and reserved Device policy defaults, allowed grants, fixed client profiles, and option availability in tests/OpenIdentityStack.Domain.Tests/Applications/ApplicationTypePolicyTests.cs
- [x] T130 [P] [POLICY] Add domain tests rejecting application type changes after creation in tests/OpenIdentityStack.Domain.Tests/Applications/ApplicationTypePolicyTests.cs
- [x] T131 [P] [POLICY] Add application/use-case tests that create/configure rejects disallowed grant, client profile, PKCE, consent, redirect, post-logout redirect, and credential combinations per type in tests/OpenIdentityStack.Application.Tests/Applications/ApplicationTypePolicyUseCaseTests.cs
- [x] T132 [P] [POLICY] Add API workflow tests for valid default create/configure requests by type and invalid matrix combinations returning validation errors in tests/OpenIdentityStack.Api.Tests/Admin/Applications/ApplicationTypePolicyEndpointTests.cs
- [x] T133 [P] [POLICY] Add contract tests for application type policy response shape and validation error examples in tests/OpenIdentityStack.Contract.Tests/Admin/Applications/ApplicationTypePolicyContractTests.cs
- [x] T134 [P] [POLICY] Add AdminWeb API client tests for fetching application type policies and applying availability metadata in src/OpenIdentityStack.AdminWeb/src/features/applications/api/applications-api.test.ts
- [x] T135 [P] [POLICY] Add AdminWeb form tests for type-specific railroading: Web, Single Page, Native, Machine-to-machine, and reserved Device controls in src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationTypePolicyForm.test.tsx
- [x] T136 [P] [POLICY] Add AdminWeb credential UI tests ensuring secret/certificate controls are hidden for public profiles and shown only for confidential profiles in src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationCredentials.test.tsx

### Implementation for Application Type Policy

- [x] T137 [POLICY] Add `ApplicationOptionAvailability`, `ApplicationOptionKey`, `ApplicationTypePolicy`, and `ClientProfile` domain types in src/OpenIdentityStack.Domain/Applications/
- [x] T138 [POLICY] Add `ApplicationTypePolicyCatalog` or equivalent domain service implementing `application-type-options-matrix.md` defaults and availability metadata in src/OpenIdentityStack.Domain/Applications/
- [x] T139 [POLICY] Update `Application` creation/configuration methods to validate against `ApplicationTypePolicyCatalog` instead of scattered type/grant checks in src/OpenIdentityStack.Domain/Applications/Application.cs
- [x] T140 [POLICY] Block application type changes after creation at the domain/API boundary and document future migration workflow requirements in src/OpenIdentityStack.Domain/Applications/Application.cs and src/OpenIdentityStack.Api/Applications/ApplicationRequests.cs
- [x] T141 [POLICY] Update create/configure command validation to pass policy violations through `DomainError.Validation` with deterministic error codes in src/OpenIdentityStack.Infrastructure/Applications/ApplicationLifecycleUseCases.cs
- [x] T142 [POLICY] Add application policy query contracts and DTOs in src/OpenIdentityStack.Application/Applications/Queries/ApplicationTypePolicyDetails.cs
- [x] T143 [POLICY] Add API response DTOs for option availability, default client profile, allowed/default grants, required redirect behavior, PKCE/consent defaults, and advanced option flags in src/OpenIdentityStack.Api/Applications/ApplicationRequests.cs
- [x] T144 [POLICY] Add `/api/admin/applications/policies/types` endpoint returning policy metadata for all application types in src/OpenIdentityStack.Api/Applications/ApplicationsApi.cs or src/OpenIdentityStack.Api/Applications/ApplicationPoliciesApi.cs
- [x] T145 [POLICY] Update `/api/admin/applications` create and `/api/admin/applications/{id}/oauth` configure endpoints to enforce policy and return clear validation responses for matrix violations in src/OpenIdentityStack.Api/Applications/ApplicationsApi.cs
- [x] T146 [POLICY] Keep advanced matrix options (`private_key_jwt`, mTLS, JWKS, DPoP, token lifetime overrides, confidential Device behavior) as `Advanced` metadata only; do not add working protocol behavior in src/OpenIdentityStack.Domain/Applications/ and src/OpenIdentityStack.Api/Applications/
- [x] T147 [POLICY] Update OpenAPI contract with application type policy endpoint and policy-driven validation semantics in specs/006-unify-applications-model/contracts/applications.openapi.yaml and tests/OpenIdentityStack.Contract.Tests/Admin/Applications/applications.openapi.yaml
- [x] T148 [POLICY] Update AdminWeb application API/types to fetch and type policy metadata in src/OpenIdentityStack.AdminWeb/src/features/applications/api/applications-api.ts and src/OpenIdentityStack.AdminWeb/src/types/index.ts
- [x] T149 [POLICY] Add AdminWeb hooks for application type policies in src/OpenIdentityStack.AdminWeb/src/features/applications/hooks/useApplicationTypePolicies.ts
- [x] T150 [POLICY] Refactor AdminWeb application form into a type-first flow that derives visible fields, fixed defaults, allowed grants, PKCE/consent controls, redirect fields, and credential hints from policy metadata in src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationForm.tsx
- [x] T151 [POLICY] Add Native redirect URI guidance and validation hints for claimed HTTPS, private scheme, and loopback redirect patterns in src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationForm.tsx
- [x] T152 [POLICY] Add Single Page browser-origin guidance and hide confidential credential controls in src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationForm.tsx and src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationCredentials.tsx
- [x] T153 [POLICY] Add Machine-to-machine railroading so only `client_credentials`, confidential profile, no redirects, no consent, and credential-management actions are presented in src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationForm.tsx
- [x] T154 [POLICY] Show reserved Device type as unavailable unless the device authorization flow is implemented and tested in src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationForm.tsx
- [x] T155 [POLICY] Update administrator docs to describe application type choices, fixed defaults, hidden unavailable options, and advanced metadata-only options in docs/admin-applications.md
- [x] T156 [POLICY] Update quickstart validation with policy API and UI railroading smoke scenarios in specs/006-unify-applications-model/quickstart.md
- [x] T157 [POLICY] Re-run build plus affected domain, application, API, contract, AdminWeb policy/form, and docs validation after policy implementation

**Checkpoint**: Application type choices are safe by construction when API calls enforce the matrix and AdminWeb guides administrators through only sensible options for each type.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup completion and blocks all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational; provides MVP unified application management.
- **User Story 2 (Phase 4)**: Depends on Foundational and can start independently, but token validation and AdminWeb detail tabs integrate with US1 artifacts.
- **User Story 3 (Phase 5)**: Depends on Foundational and is safest after US1 application persistence/use cases exist; migration integrates with US1 use cases and US2 credential mapping.
- **Polish (Phase 6)**: Depends on completed target user stories.
- **Breaking-change alignment (Phase 7)**: Depends on completed target user stories and supersedes already-created compatibility code.
- **Application type policy (Phase 8)**: Depends on the unified Applications domain/API/AdminWeb from US1/US2 and should run after Phase 7 so policy work is not duplicated across removed compatibility surfaces.

### User Story Dependencies

- **US1 Manage one application model**: No dependency on US2 or US3 after Foundation; recommended MVP.
- **US2 Manage machine-to-machine applications safely**: Requires shared Application/ApplicationCredential foundation; can be developed in parallel with US1 domain/use-case work but checkpoint validation needs application API/detail integration.
- **US3 Migrate existing registrations with continuity**: Requires Application persistence contracts; migration and cleanup depend on US1 use cases and US2 credential mapping for full coverage.
- **Breaking-change alignment**: Depends on completed US1/US2/US3 implementation and supersedes earlier compatibility adapter tasks T084-T086, T095-T098, T101, and T106.
- **Application type policy**: Depends on US1 create/configure flows and US2 credential surfaces; it tightens rules for all application types and adds policy-driven UI behavior.

### Within Each User Story

- Write tests first and confirm they fail.
- Implement domain model before application use cases.
- Implement use cases before infrastructure adapters and API endpoints.
- Implement API contract before AdminWeb integration.
- Add security, audit, validation, and safe error behavior before checkpoint validation.

## Parallel Opportunities

- Setup tasks T002-T005 can run in parallel after T001 path decisions are accepted.
- Foundational tasks T006-T015 and T017-T018 can run in parallel; T016, T019, and T020 integrate them.
- US1 tests T021-T029 can run in parallel before implementation.
- US1 domain/application/API/AdminWeb implementation has parallel lanes: T030-T034, T037-T041, T043-T045, and T046-T050.
- US2 tests T054-T061 can run in parallel before implementation.
- US2 credential domain/application/API/AdminWeb lanes T062-T066, T067-T075, and T076-T079 can run in parallel after domain contracts stabilize.
- US3 migration tests T080-T087 can run in parallel before implementation.
- US3 migration, removed-endpoint API behavior, AdminWeb route cleanup, and docs tasks T088-T101/T117-T128 can run as separate lanes after the shared application use cases exist.
- Polish tasks T102-T108 can run in parallel; validation tasks T111-T116 should run after implementation stabilization.
- Phase 8 policy tests T129-T136 can run in parallel before implementation; backend policy tasks T137-T147 and AdminWeb railroading tasks T148-T154 can proceed in parallel after the policy contract stabilizes.

## Parallel Example: User Story 1

```text
Task: "T021 Add domain tests for application creation, client ID validation, display name validation, metadata updates, enable/disable, and delete behavior in tests/OpenIdentityStack.Domain.Tests/Applications/ApplicationTests.cs"
Task: "T026 Add API workflow tests for create/get/list/update/configure/disable/enable/delete endpoints in tests/OpenIdentityStack.Api.Tests/Admin/Applications/ApplicationsEndpointWorkflowTests.cs"
Task: "T028 Add AdminWeb API client tests for applications list/get/create/update/delete calls in src/OpenIdentityStack.AdminWeb/src/features/applications/api/applications-api.test.ts"
```

## Parallel Example: User Story 2

```text
Task: "T054 Add domain tests for ApplicationCredential secret/certificate creation, revocation, expiration, active state, and public-application rejection in tests/OpenIdentityStack.Domain.Tests/Applications/ApplicationCredentialTests.cs"
Task: "T057 Add infrastructure identity tests for application client authentication handler secret/certificate/disabled/revoked/expired behavior in tests/OpenIdentityStack.Infrastructure.Tests/Identity/ApplicationClientAuthenticationHandlerTests.cs"
Task: "T061 Add AdminWeb tests for application credential dialogs, one-time secret display, and public credential rejection messages in src/OpenIdentityStack.AdminWeb/src/features/applications/components/ApplicationCredentials.test.tsx"
```

## Parallel Example: User Story 3

```text
Task: "T080 Add migration preflight tests for duplicate client IDs, invalid service-account grants, ambiguous client profile review, and no-mutation failure behavior in tests/OpenIdentityStack.Infrastructure.Tests/Persistence/Applications/ApplicationMigrationPreflightTests.cs"
Task: "T117 Update API tests so removed legacy endpoints return 404 in tests/OpenIdentityStack.Api.Tests/Admin/Applications/"
Task: "T127 Update docs, README, deploy guidance, and quickstart to describe removed legacy endpoints instead of compatibility windows or deprecation headers"
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 setup tasks T001-T005.
2. Complete Phase 2 foundation tasks T006-T020.
3. Complete Phase 3 US1 tasks T021-T053.
4. Stop and validate unified application CRUD/status/configuration through tests and AdminWeb baseline flows.
5. Demo the unified Applications model without credential lifecycle or migration cleanup.

### Incremental Delivery

1. Deliver US1 to establish the unified application model and `/api/admin/applications`.
2. Deliver US2 to harden machine-to-machine application and credential behavior.
3. Deliver US3 to migrate supported legacy data.
4. Complete Phase 7 breaking-change alignment to remove compatibility code introduced earlier.
5. Complete Phase 8 application type policy enforcement and AdminWeb railroading.
6. Complete Phase 6 hardening, documentation, and validation before release.

### Parallel Team Strategy

1. Team completes setup/foundation together.
2. Backend domain/application lane owns T021-T037 and T054-T068.
3. Persistence/OpenIddict lane owns T038-T042, T069-T071, and T080-T093.
4. API/contract lane owns T026-T027, T043-T045, T058-T060, T084-T097, and breaking cleanup T117-T121.
5. Policy/backend lane owns T129-T147.
6. AdminWeb lane owns T028-T029, T046-T052, T061, T076-T079, T087-T100, T126, and T148-T154.
7. Docs/ops lane owns T098, T101-T105, T127, T155-T156, and final validation coordination T111-T116/T128/T157.

## Notes

- Use direct use cases/query handlers and explicit DTO mapping; do not introduce MediatR or AutoMapper.
- Use System.Text.Json, Microsoft OpenAPI, and Scalar patterns; do not introduce Newtonsoft.Json or Swashbuckle/Swagger packages.
- Keep domain code independent from EF Core, ASP.NET Core, OpenIddict, and React.
- Plain secrets and secret hashes must never be logged, returned outside one-time secret responses, exposed in AdminWeb after dismissal, or included in audit payloads.
- Legacy client/service-account admin endpoints are removed in this pre-1.0 breaking change; do not add compatibility adapters.
- API policy enforcement is authoritative; AdminWeb railroading improves UX but must never be the only business-rule check.
- Advanced matrix options are represented as unavailable/advanced policy metadata only until explicit protocol support and tests are added.
