# Quick start

Use this quick start to run OpenIdentityStack end-to-end on your local machine.

```bash
git clone https://github.com/Tjeerd-menno/open-identity-stack.git
cd open-identity-stack

dotnet restore OpenIdentityStack.slnx
dotnet build OpenIdentityStack.slnx --no-restore

cd src/OpenIdentityStack.AppHost
dotnet run
```

When AppHost starts, it launches:

- PostgreSQL
- DbMigrator
- API
- Admin UI

Open the URLs printed by the AppHost process and confirm:

- admin UI is reachable
- API responds

If you need a clean environment for repeated local runs, start with:

```text
OPENIDENTITYSTACK_DISABLE_DATA_VOLUME=true
```

And restart AppHost.

## Validate quickly

1. Create a first admin user.
2. Login to the admin UI.
3. Open the users or clients section and verify items are listed.

