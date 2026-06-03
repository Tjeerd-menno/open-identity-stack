# Quickstart: Management Web AdminWeb Parity

## Goal

Validate the ManagementWeb shared foundation, AdminWeb parity slices, consolidated Applications workflows, the ManagementWeb Audit slice, and cross-UI sign-in continuity.

## Prerequisites

- .NET 10 SDK
- Node/npm
- Existing Admin API and Aspire-based local backend
- Browser for frontend and E2E validation

## Baseline validation

```powershell
dotnet restore OpenIdentityStack.slnx
dotnet build OpenIdentityStack.slnx --no-restore
```

## Backend and contract validation

```powershell
dotnet test --project tests\OpenIdentityStack.Domain.Tests\OpenIdentityStack.Domain.Tests.csproj
dotnet test --project tests\OpenIdentityStack.Application.Tests\OpenIdentityStack.Application.Tests.csproj
dotnet test --project tests\OpenIdentityStack.Infrastructure.Tests\OpenIdentityStack.Infrastructure.Tests.csproj
dotnet test --test-modules "tests/**/bin/Debug/net10.0/*Api.Tests.dll" --max-parallel-test-modules 1 --no-build --no-restore
dotnet test --test-modules "tests/**/bin/Debug/net10.0/*Contract.Tests.dll" --max-parallel-test-modules 1 --no-build --no-restore
```

For Audit endpoint work, also run the focused API/contract suites that include `Audit` tests after they are added.

## AdminWeb regression validation

```powershell
Set-Location src\OpenIdentityStack.AdminWeb
npm install
npm run build
npm run lint
npm test
Set-Location ..\..
```

## ManagementWeb validation

```powershell
Set-Location src\OpenIdentityStack.ManagementWeb
npm install
npm run build
npm run lint
npm test
Set-Location ..\..
```

## ManagementWeb E2E validation

```powershell
dotnet test --project tests\OpenIdentityStack.ManagementWeb.E2ETests\OpenIdentityStack.ManagementWeb.E2ETests.csproj
```

Run focused E2E specs for each completed slice during development:

- `ApplicationManagementTests`
- `UserManagementTests`
- `RoleManagementTests`
- `GroupManagementTests`
- `SessionManagementTests`
- `ProviderManagementTests`
- `SettingsManagementTests`
- `ApplicationPermissionsManagementTests`
- `AuditEntryManagementTests`
- `OverviewSmokeTests`
- `AuthContinuityTests`

## Local runtime check

```powershell
dotnet run --project src\OpenIdentityStack.AppHost
```

Use the Aspire dashboard to verify that backend services, AdminWeb, and ManagementWeb resources start cleanly.

## Manual smoke checks

- ManagementWeb navigation includes Overview, Users, Roles, Groups, Applications, Permissions, Sessions, Identity providers, Settings, and Audit.
- ManagementWeb navigation does not include Clients or Service Accounts.
- Applications uses one list and consolidated `/api/admin/applications` behavior.
- Audit lists paged records from `/api/admin/audit-entries` and can expand details.
- AdminWeb remains independently reachable during rollout.

## Documentation check

```powershell
python -m mkdocs build --strict
```
