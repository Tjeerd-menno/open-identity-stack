# Quickstart: Unify Applications Domain

## Goal

Validate the unified Applications implementation from domain rules through admin API, migration, OpenIddict token behavior, AdminWeb, and docs.

## Prerequisites

- .NET 10 SDK from `global.json`.
- Node/npm for AdminWeb validation.
- Python with mkdocs dependencies available for docs validation when docs are changed.
- PostgreSQL/Aspire available for integration-style runs.

## Baseline restore and build

```powershell
dotnet restore OpenIdentityStack.slnx
dotnet build OpenIdentityStack.slnx --no-restore
```

## Backend validation

Run focused suites while developing:

```powershell
dotnet test --project tests\OpenIdentityStack.Domain.Tests\OpenIdentityStack.Domain.Tests.csproj
dotnet test --project tests\OpenIdentityStack.Application.Tests\OpenIdentityStack.Application.Tests.csproj
dotnet test --project tests\OpenIdentityStack.Infrastructure.Tests\OpenIdentityStack.Infrastructure.Tests.csproj
```

Run API-style modules with the repository's sequential pattern after building:

```powershell
dotnet test --test-modules "tests/**/bin/Debug/net10.0/*Api.Tests.dll" --max-parallel-test-modules 1 --no-build --no-restore
dotnet test --test-modules "tests/**/bin/Debug/net10.0/*Contract.Tests.dll" --max-parallel-test-modules 1 --no-build --no-restore
```

## Migration validation scenarios

Validate these data cases before rollout:

1. Existing clients migrate to applications and preserve `client_id`.
2. Existing service accounts migrate to `MachineToMachine` applications and preserve `client_id`.
3. Existing service-account secrets migrate to application client-secret credentials.
4. Existing service-account certificates migrate to application certificate credentials.
5. Duplicate `client_id` values across clients and service accounts fail preflight before mutation.
6. Service accounts with non-`client_credentials` grants fail strict production preflight.
7. Legacy permissions map to `applications:*` permissions without over-granting or under-granting.
8. Legacy client/service-account tables are removed only after supported data has moved into Applications.

## Admin API smoke scenarios

After implementation, verify through the admin API:

1. Call `GET /api/admin/applications/policies/profiles`; confirm Web, Single Page, Native, Machine-to-machine, and reserved Device policy entries are returned with fixed client profile, default grants, option availability, and default PKCE/consent flags.
2. Create a machine-to-machine application with an initial secret; confirm the secret is returned once.
3. Reject machine-to-machine applications that include redirect URIs, post-logout redirect URIs, consent, PKCE, or interactive grants.
4. Reject adding a secret or certificate to a public application.
5. Reject manual creation of reserved `Device` applications until the device authorization flow is implemented.
6. Reject attempts to change `Application.Type` during OAuth configuration.
7. Rotate a secret with `revokeExisting = true`; confirm old credential fails and new credential works.
8. Disable an application; confirm new token issuance fails and admin read still works.
9. List applications with `type`, `status`, `clientType`, and `search` filters.
10. Call `/api/admin/clients` and `/api/admin/service-accounts`; confirm removed legacy routes return `404 Not Found`.

## AdminWeb validation

```powershell
Set-Location src\OpenIdentityStack.AdminWeb
npm install
npm run build
npm run lint
npm test
Set-Location ..\..
```

Add these focused UI smoke checks while validating the policy-driven form:

1. Web starts with confidential profile, `authorization_code`, redirect fields, PKCE, and consent visible.
2. Single Page starts with public profile, PKCE fixed on, and credential-management actions hidden.
3. Native shows claimed HTTPS, private-scheme, and loopback redirect guidance.
4. Machine-to-machine stays locked to `client_credentials`, hides redirect and consent controls, and exposes credential-management actions only after creation.
5. Device remains visible as a reserved disabled type with explanatory messaging.

Run E2E coverage when application management UI flows are implemented:

```powershell
dotnet test --project tests\OpenIdentityStack.AdminWeb.E2ETests\OpenIdentityStack.AdminWeb.E2ETests.csproj
```

## Local Aspire validation

```powershell
dotnet run --project src\OpenIdentityStack.AppHost
```

Use the Aspire dashboard to verify the API, database, migration behavior, logs, and AdminWeb resources start cleanly.

## Documentation validation

```powershell
python -m mkdocs build --strict
```

## Rollout checklist

1. Run migration preflight in strict mode and remediate all conflicts before production migration.
2. Deploy application code with only the new application endpoints enabled.
3. Update AdminWeb to use `/api/admin/applications`.
4. Monitor audit logs and token endpoint validation failures for disabled/revoked/expired credential behavior.
5. Announce that `/api/admin/clients` and `/api/admin/service-accounts` were removed and `/api/admin/applications` is the supported replacement.
6. Remove old tables after verification that Applications and OpenIddict projection are authoritative.
