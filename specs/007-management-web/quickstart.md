# Quickstart: Management Web Foundation

## Goal

Validate the new Management Web shell, theme behavior, Users slice, and cross-UI sign-in continuity.

## Prerequisites

- .NET 10 SDK
- Node/npm
- Existing Admin API and Aspire-based local backend
- Browser for frontend validation

## Baseline validation

```powershell
dotnet restore OpenIdentityStack.slnx
dotnet build OpenIdentityStack.slnx --no-restore
```

## Frontend validation

```powershell
Set-Location src\OpenIdentityStack.AdminWeb
npm install
npm run build
npm run lint
npm test
Set-Location ..\..
```

After Management Web is added, run the same validation there:

```powershell
Set-Location src\OpenIdentityStack.ManagementWeb
npm install
npm run build
npm run lint
npm test
Set-Location ..\..
```

## Backend and contract validation

```powershell
dotnet test --project tests\OpenIdentityStack.Domain.Tests\OpenIdentityStack.Domain.Tests.csproj
dotnet test --project tests\OpenIdentityStack.Application.Tests\OpenIdentityStack.Application.Tests.csproj
dotnet test --project tests\OpenIdentityStack.Infrastructure.Tests\OpenIdentityStack.Infrastructure.Tests.csproj
dotnet test --test-modules "tests/**/bin/Debug/net10.0/*Api.Tests.dll" --max-parallel-test-modules 1 --no-build --no-restore
dotnet test --test-modules "tests/**/bin/Debug/net10.0/*Contract.Tests.dll" --max-parallel-test-modules 1 --no-build --no-restore
```

## Local runtime check

```powershell
dotnet run --project src\OpenIdentityStack.AppHost
```

Use the Aspire dashboard to verify that the backend services, AdminWeb, and Management Web resources start cleanly.

## Documentation check

```powershell
python -m mkdocs build --strict
```
