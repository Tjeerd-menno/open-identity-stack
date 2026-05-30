# Quickstart: Application Permission Registry

This quickstart defines expected validation flow for `006`. Exact commands should be narrowed per slice when tasks are generated.

## Prerequisites

- Clean database/destructive reset. `006` does not preserve arbitrary pre-existing role permission strings.
- .NET 10 SDK.
- AdminWeb dependencies installed.
- Aspire/container runtime available for AdminWeb E2E.

## Deployment Note

`006` is a pre-1.0 alpha breaking change. Developer and deployment environments should plan a clean database reset before enabling the application permission registry implementation from this spec. Existing arbitrary role permission strings are not treated as normal valid data; later diagnostics may report them as integrity issues for remediation.

## Slice 1: Application Registration And Inline Manifest Management

Backend validation:

```powershell
dotnet build OpenIdentityStack.slnx --no-restore
dotnet test --project tests/OpenIdentityStack.Domain.Tests/OpenIdentityStack.Domain.Tests.csproj --filter-class ApplicationPermissionManifestTests --no-restore
dotnet test --project tests/OpenIdentityStack.Application.Tests/OpenIdentityStack.Application.Tests.csproj --filter-class ApplicationPermissionManifestUseCaseTests --no-restore
dotnet test --project tests/OpenIdentityStack.Api.Tests/OpenIdentityStack.Api.Tests.csproj --filter-class ApplicationPermissionsApiTests --no-restore
dotnet test --project tests/OpenIdentityStack.Contract.Tests/OpenIdentityStack.Contract.Tests.csproj --filter-class ApplicationPermissionsContractTests --no-restore
```

AdminWeb validation:

```powershell
cd src/OpenIdentityStack.AdminWeb
npm run build
npm run lint
npm test
```

E2E validation:

```powershell
dotnet run --project src/OpenIdentityStack.AppHost
dotnet test --project tests/OpenIdentityStack.AdminWeb.E2ETests/OpenIdentityStack.AdminWeb.E2ETests.csproj --filter-class ApplicationPermissionsManifestE2ETests --no-restore
```

Manual smoke:

1. Open AdminWeb.
2. Navigate to `Application Permissions`.
3. Create a permission application from inline JSON manifest.
4. View list/detail with owner, maintainers, status, manifest version, and permissions.
5. Apply a strictly newer non-destructive manifest update.
6. Verify same/older version returns validation error.
7. Verify omitted existing permission returns `DestructiveManifestChangeNotSupportedYet` until slice 3.

## Slice 2: Role Picker, Validation, Broad Grants, And Emission

Backend validation adds:

```powershell
dotnet test --project tests/OpenIdentityStack.Application.Tests/OpenIdentityStack.Application.Tests.csproj --filter-class PermissionAssignmentValidationTests --no-restore
dotnet test --project tests/OpenIdentityStack.Api.Tests/OpenIdentityStack.Api.Tests.csproj --filter-class RolePermissionAssignmentApiTests --no-restore
dotnet test --project tests/OpenIdentityStack.Api.Tests/OpenIdentityStack.Api.Tests.csproj --filter-class AuthorizationControllerTests --filter-method Introspection --no-restore
dotnet test --project tests/OpenIdentityStack.Contract.Tests/OpenIdentityStack.Contract.Tests.csproj --filter-class RolePermissionsContractTests --no-restore
```

Manual smoke:

1. Verify platform permissions and dynamic application permissions appear in the role picker.
2. Verify aggregate wildcard appears above its aggregate as "All current and future ... permissions".
3. Try assigning wildcard without acknowledgement and confirm a structured conflict.
4. Acknowledge and assign wildcard.
5. Verify role detail stores the wildcard string.
6. Verify token/introspection permission emission contains concrete permissions only.

## Slice 3: Destructive Workflows

Backend validation adds destructive manifest/delete and transaction tests:

```powershell
dotnet test --project tests/OpenIdentityStack.Application.Tests/OpenIdentityStack.Application.Tests.csproj --filter-class ApplicationPermissionDestructiveChangeTests --no-restore
dotnet test --project tests/OpenIdentityStack.Api.Tests/OpenIdentityStack.Api.Tests.csproj --filter-class ApplicationPermissionDeletionApiTests --no-restore
```

Manual smoke:

1. Preview a newer manifest that omits an assigned permission.
2. Confirm exact assignments and wildcard impacts/collapses are shown.
3. Apply destructive update as admin.
4. Verify assignments are removed, tombstone exists only in explicit history, and audit contains details.

## Slice 4: Remote Import

Manual smoke:

1. Configure trusted `manifestBaseUrl`.
2. Serve a controlled local fixture manifest.
3. Preview remote import.
4. Apply remote import.
5. Verify unsafe remote fetch cases reject in API tests.

## Slice 5: Tombstone History And Diagnostics

Manual smoke:

1. Open removed permission history.
2. Add replacement guidance.
3. Run diagnostics and verify integrity issues are shown only as remediation data.
