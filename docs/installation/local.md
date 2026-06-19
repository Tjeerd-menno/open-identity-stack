# Local installation

Use this path for evaluation, development, and integration testing.

## Prerequisites

- .NET 10 SDK
- Node.js LTS
- Git

PostgreSQL client tooling is optional but useful for diagnostics.

## Run locally with Aspire

```bash
git clone https://github.com/Tjeerd-menno/open-identity-stack.git
cd open-identity-stack
dotnet restore OpenIdentityStack.slnx
dotnet build OpenIdentityStack.slnx --no-restore
cd src/OpenIdentityStack.AppHost
dotnet run
```

The AppHost composes:

- PostgreSQL
- the database migrator
- the API
- the management web app

## Local runtime behavior

- PostgreSQL uses a persistent data volume by default
- `Seed__DevelopmentData=true` is set for the local migrator path
- the management web receives `VITE_OIDC_AUTHORITY` and `VITE_API_BASE_URL` from the API endpoint

## Useful local toggles

### Clean state between runs

```text
OPENIDENTITYSTACK_DISABLE_DATA_VOLUME=true
```

### Skip the management web

```text
OPENIDENTITYSTACK_ENABLE_MANAGEMENTWEB=false
```

## First-run checklist

1. confirm the migrator completes
2. open the management web
3. confirm the API root or health endpoints respond
4. sign in and verify that an admin page loads

Continue with [Quick start](../getting-started/quick-start.md) if you want the fastest end-to-end path.
