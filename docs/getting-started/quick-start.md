# Quick start

Use this path to run OpenIdentityStack end-to-end on a local machine with the bundled Aspire composition.

## Start the stack

```bash
git clone https://github.com/Tjeerd-menno/open-identity-stack.git
cd open-identity-stack

dotnet restore OpenIdentityStack.slnx
dotnet build OpenIdentityStack.slnx --no-restore

cd src/OpenIdentityStack.AppHost
dotnet run
```

When AppHost starts, it provisions or connects:

- PostgreSQL
- DbMigrator
- API
- Admin web UI

## Use a disposable local database when needed

If you want a clean state between runs:

```text
OPENIDENTITYSTACK_DISABLE_DATA_VOLUME=true
```

Set the variable before starting AppHost.

## First validation

Confirm the following in the AppHost output and browser:

1. the API responds and health endpoints come up
2. the management web loads
3. the migrator completes
4. you can reach the sign-in path

## What to do next

After the stack is running:

1. sign in with your seeded or configured admin account
2. verify that users, roles, clients, or service accounts can be listed
3. register the first client you want to test
4. use [Web app OIDC integration](../integrations/web-app-oidc.md) or [Machine-to-machine access](../integrations/machine-to-machine.md) for the first consumer
