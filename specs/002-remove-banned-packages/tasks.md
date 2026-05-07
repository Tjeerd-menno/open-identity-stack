# Refactoring Tasks: Remove Banned Packages

**Feature**: Remove MediatR and Swashbuckle, replace with direct injection and Scalar  
**Created**: 2026-01-20  
**Status**: Completed  
**Rationale**: Per plan.md constraints, MediatR and Swashbuckle are explicitly disallowed

## Summary

This refactoring removes two banned packages:
1. **MediatR** → Replace with direct use-case/query handler injection
2. **Swashbuckle.AspNetCore** → Replace with Scalar.AspNetCore for API documentation

---

## Phase 1: Setup

- [X] T001 Add Scalar.AspNetCore package to Directory.Packages.props
- [X] T002 Add Scalar.AspNetCore PackageReference to src/OpenIdentityStack.Api/OpenIdentityStack.Api.csproj

---

## Phase 2: Replace Swashbuckle with Scalar

- [X] T003 Remove Swashbuckle.AspNetCore from Directory.Packages.props
- [X] T004 Update Program.cs to use Scalar API reference UI instead of Swagger in src/OpenIdentityStack.Api/Program.cs
- [X] T005 Update README.md to reference Scalar instead of Swagger at /scalar/v1

---

## Phase 3: Create Query Handler Interfaces (Application Layer)

Convert MediatR IRequest/IRequestHandler patterns to direct interfaces following existing use case pattern.

### Groups Queries
- [X] T010 [P] Create IGetGroupQueryHandler interface in src/OpenIdentityStack.Application/Groups/Queries/IGetGroupQueryHandler.cs
- [X] T011 [P] Create IListGroupsQueryHandler interface in src/OpenIdentityStack.Application/Groups/Queries/IListGroupsQueryHandler.cs
- [X] T012 [P] Create IListGroupMembersQueryHandler interface in src/OpenIdentityStack.Application/Groups/Queries/IListGroupMembersQueryHandler.cs
- [X] T013 [P] Create IListGroupMappingsQueryHandler interface in src/OpenIdentityStack.Application/Groups/Queries/IListGroupMappingsQueryHandler.cs
- [X] T014 [P] Create IGetUserGroupsQueryHandler interface in src/OpenIdentityStack.Application/Groups/Queries/IGetUserGroupsQueryHandler.cs
- [X] T015 [P] Create IGetGroupClaimsForUserQueryHandler interface in src/OpenIdentityStack.Application/Groups/Queries/IGetGroupClaimsForUserQueryHandler.cs

### Users Queries
- [X] T020 [P] Create IGetUserEffectiveRolesQueryHandler interface in src/OpenIdentityStack.Application/Users/Queries/IGetUserEffectiveRolesQueryHandler.cs

### Roles Queries
- [X] T025 [P] Create IGetUserRolesQueryHandler interface in src/OpenIdentityStack.Application/Roles/Queries/IGetUserRolesQueryHandler.cs

### ServiceAccounts Queries
- [X] T030 [P] Create IGetServiceAccountQueryHandler interface in src/OpenIdentityStack.Application/ServiceAccounts/Queries/IGetServiceAccountQueryHandler.cs
- [X] T031 [P] Create IListServiceAccountsQueryHandler interface in src/OpenIdentityStack.Application/ServiceAccounts/Queries/IListServiceAccountsQueryHandler.cs

### Sessions Commands
- [X] T035 [P] Create IProcessLogoutUseCase interface (convert from IRequestHandler) in src/OpenIdentityStack.Application/Sessions/Commands/IProcessLogoutUseCase.cs
- [X] T036 [P] Create INotifyClientsOfLogoutUseCase interface in src/OpenIdentityStack.Application/Sessions/Commands/INotifyClientsOfLogoutUseCase.cs

---

## Phase 4: Update Query/Command Handlers to Implement Interfaces

Remove MediatR IRequestHandler inheritance, implement new interfaces.

### Groups
- [X] T040 [P] Refactor GetGroupQueryHandler to implement IGetGroupQueryHandler in src/OpenIdentityStack.Application/Groups/Queries/GroupQueries.cs
- [X] T041 [P] Refactor ListGroupsQueryHandler to implement IListGroupsQueryHandler in src/OpenIdentityStack.Application/Groups/Queries/GroupQueries.cs
- [X] T042 [P] Refactor ListGroupMembersQueryHandler to implement IListGroupMembersQueryHandler in src/OpenIdentityStack.Application/Groups/Queries/GroupMemberQueries.cs
- [X] T043 [P] Refactor ListGroupMappingsQueryHandler to implement IListGroupMappingsQueryHandler in src/OpenIdentityStack.Application/Groups/Queries/GroupMemberQueries.cs
- [X] T044 [P] Refactor GetUserGroupsQueryHandler to implement IGetUserGroupsQueryHandler in src/OpenIdentityStack.Application/Groups/Queries/GroupMemberQueries.cs
- [X] T045 [P] Refactor GetGroupClaimsForUserQueryHandler to implement IGetGroupClaimsForUserQueryHandler in src/OpenIdentityStack.Application/Groups/Queries/GetGroupClaimsForUserQuery.cs

### Users
- [X] T050 [P] Refactor GetUserEffectiveRolesQueryHandler to implement IGetUserEffectiveRolesQueryHandler in src/OpenIdentityStack.Application/Users/Queries/GetUserEffectiveRolesQueryHandler.cs

### Roles
- [X] T055 [P] Refactor GetUserRolesQueryHandler to implement IGetUserRolesQueryHandler in src/OpenIdentityStack.Application/Roles/Queries/GetUserRolesQueryHandler.cs

### ServiceAccounts
- [X] T060 [P] Refactor GetServiceAccountQueryHandler to implement interface in src/OpenIdentityStack.Application/ServiceAccounts/Queries/GetServiceAccountQueryHandler.cs
- [X] T061 [P] Refactor ListServiceAccountsQueryHandler to implement interface in src/OpenIdentityStack.Application/ServiceAccounts/Queries/ListServiceAccountsQueryHandler.cs

### Sessions
- [X] T065 Refactor ProcessLogoutCommand handler to implement IProcessLogoutUseCase in src/OpenIdentityStack.Application/Sessions/Commands/ProcessLogoutCommand.cs
- [X] T066 Refactor NotifyClientsOfLogoutCommand handler to implement INotifyClientsOfLogoutUseCase in src/OpenIdentityStack.Application/Sessions/Commands/NotifyClientsOfLogoutCommand.cs

---

## Phase 5: Register New Handlers in DI

- [X] T070 Register all new query handler interfaces in ServiceCollectionExtensions.AddCommonServices() in src/OpenIdentityStack.Infrastructure/ServiceCollectionExtensions.cs

---

## Phase 6: Update Controllers to Use Direct Injection

Replace ISender with direct handler injection.

- [X] T080 Refactor UsersController to inject IGetUserEffectiveRolesQueryHandler and IGetUserGroupsQueryHandler instead of ISender in src/OpenIdentityStack.Api/Users/UsersController.cs
- [X] T081 Refactor GroupsController to inject IGetGroupQueryHandler, IListGroupsQueryHandler, IListGroupMembersQueryHandler, IListGroupMappingsQueryHandler instead of ISender in src/OpenIdentityStack.Api/Groups/GroupsController.cs
- [X] T082 Refactor AuthorizationController to inject IGetUserEffectiveRolesQueryHandler, IGetGroupClaimsForUserQueryHandler instead of ISender in src/OpenIdentityStack.Api/Authentication/AuthorizationController.cs
- [X] T083 Refactor TestSeedingController to inject required handlers instead of ISender in src/OpenIdentityStack.Api/Admin/TestSeedingController.cs

---

## Phase 7: Remove MediatR from Application Layer

- [X] T090 Remove MediatR using statements and IRequest/IRequestHandler from src/OpenIdentityStack.Application/Groups/Queries/GroupQueries.cs
- [X] T091 Remove MediatR using statements and IRequest/IRequestHandler from src/OpenIdentityStack.Application/Groups/Queries/GroupMemberQueries.cs
- [X] T092 Remove MediatR using statements and IRequest/IRequestHandler from src/OpenIdentityStack.Application/Groups/Queries/GetGroupClaimsForUserQuery.cs
- [X] T093 Remove MediatR using statements and IRequest from src/OpenIdentityStack.Application/Users/Queries/GetUserEffectiveRolesQuery.cs
- [X] T094 Remove MediatR using statements and IRequestHandler from src/OpenIdentityStack.Application/Users/Queries/GetUserEffectiveRolesQueryHandler.cs
- [X] T095 Remove MediatR using statements from src/OpenIdentityStack.Application/Roles/Queries/GetUserRolesQuery.cs
- [X] T096 Remove MediatR using statements from src/OpenIdentityStack.Application/Roles/Queries/GetUserRolesQueryHandler.cs
- [X] T097 Remove MediatR using statements from src/OpenIdentityStack.Application/ServiceAccounts/Queries/*.cs
- [X] T098 Remove MediatR using statements from src/OpenIdentityStack.Application/Sessions/Commands/ProcessLogoutCommand.cs
- [X] T099 Remove MediatR using statements from src/OpenIdentityStack.Application/Sessions/Commands/NotifyClientsOfLogoutCommand.cs

---

## Phase 8: Remove MediatR Package References

- [X] T100 Remove MediatR PackageReference from src/OpenIdentityStack.Application/OpenIdentityStack.Application.csproj
- [X] T101 Remove AddMediatR() registration from src/OpenIdentityStack.Application/DependencyInjection.cs
- [X] T102 Remove MediatR from Directory.Packages.props

---

## Phase 9: Update Tests

- [X] T110 Update AuthorizationControllerTests to mock new interfaces instead of ISender in tests/OpenIdentityStack.Api.Tests/Authentication/AuthorizationControllerTests.cs
- [X] T111 Update SessionValidationBenchmarks to use new interfaces in tests/OpenIdentityStack.Api.Tests/Performance/SessionValidationBenchmarks.cs
- [X] T112 Update AuthEndpointLatencyTests to use new interfaces in tests/OpenIdentityStack.Api.Tests/Performance/AuthEndpointLatencyTests.cs

---

## Phase 10: Validation

- [X] T120 Run dotnet build to verify no MediatR or Swashbuckle references remain
- [X] T121 Run dotnet test to verify all tests pass
- [ ] T122 Verify Scalar API docs accessible at /scalar/v1 when running locally
- [X] T123 Update .github/copilot-instructions.md to remove MediatR references

---

## Dependencies

```
Phase 1 (Setup)
    ↓
Phase 2 (Scalar) ─────────────────────────────────────────────┐
    ↓                                                          │
Phase 3 (Interfaces) [Parallel within phase]                   │
    ↓                                                          │
Phase 4 (Handler refactoring) [Parallel within phase]          │
    ↓                                                          │
Phase 5 (DI Registration)                                      │
    ↓                                                          │
Phase 6 (Controller updates)                                   │
    ↓                                                          │
Phase 7 (Remove MediatR usings) [Parallel within phase]        │
    ↓                                                          │
Phase 8 (Remove packages)                                      │
    ↓                                                          │
Phase 9 (Update tests)                                         │
    ↓                                                          │
Phase 10 (Validation) ←────────────────────────────────────────┘
```

---

## Files Affected

### Packages
- `Directory.Packages.props` - Remove MediatR, Swashbuckle; Add Scalar
- `src/OpenIdentityStack.Application/OpenIdentityStack.Application.csproj` - Remove MediatR
- `src/OpenIdentityStack.Api/OpenIdentityStack.Api.csproj` - Add Scalar

### Application Layer (MediatR removal)
- `src/OpenIdentityStack.Application/DependencyInjection.cs`
- `src/OpenIdentityStack.Application/Groups/Queries/*.cs`
- `src/OpenIdentityStack.Application/Users/Queries/*.cs`
- `src/OpenIdentityStack.Application/Roles/Queries/*.cs`
- `src/OpenIdentityStack.Application/ServiceAccounts/Queries/*.cs`
- `src/OpenIdentityStack.Application/Sessions/Commands/*.cs`

### API Layer
- `src/OpenIdentityStack.Api/Program.cs` - Scalar setup
- `src/OpenIdentityStack.Api/Users/UsersController.cs`
- `src/OpenIdentityStack.Api/Groups/GroupsController.cs`
- `src/OpenIdentityStack.Api/Authentication/AuthorizationController.cs`
- `src/OpenIdentityStack.Api/Admin/TestSeedingController.cs`

### Tests
- `tests/OpenIdentityStack.Api.Tests/Authentication/AuthorizationControllerTests.cs`
- `tests/OpenIdentityStack.Api.Tests/Performance/*.cs`

### Documentation
- `README.md`
- `.github/copilot-instructions.md`
