# Local installation

This path is for evaluation, development, and local integration testing.

## Prerequisites

- .NET 10 SDK
- Node.js LTS
- Git

## Run locally

```bash
git clone https://github.com/Tjeerd-menno/open-identity-stack.git
cd open-identity-stack
dotnet restore OpenIdentityStack.slnx
dotnet build OpenIdentityStack.slnx --no-restore
cd src/OpenIdentityStack.AppHost
dotnet run
```

AppHost manages all required services for local use.

## First run defaults

- PostgreSQL is provisioned/managed through Aspire.
- DbMigrator runs automatically from local composition.
- Admin UI and API are started together with the AppHost model.

## Optional settings for local experiments

- `OPENIDENTITYSTACK_DISABLE_DATA_VOLUME=true`
  - Use this to avoid persistent PostgreSQL state between runs.

## Environment notes

- If you run into certificate issues in local development, use a dedicated development
  certificate workflow before switching to production secret-backed certificates.
- Keep local secrets in environment files outside source control.

