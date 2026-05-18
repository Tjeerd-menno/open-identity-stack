# Guided setup

Use this path to pick the right installation path quickly.

## Step 1: Choose your deployment style

1. **Local evaluation**
   - Use Aspire and local PostgreSQL for development and demos.
   - Keep configuration minimal and let Aspire provision dependencies.
2. **Production**
   - Use Kubernetes/OpenShift or a managed container path.
   - Bring your own TLS certificates and database connectivity.
3. **Windows service**
   - Use only if your environment requires `.NET` services without container runtime.

## Step 2: Check prerequisites

Minimum prerequisites for local install:

- .NET SDK (10.0)
- Node.js (LTS)
- Git
- PostgreSQL client tooling (optional for diagnostics)

## Step 3: Local-first bootstrap

1. Clone and build the solution.
2. Run AppHost to start the API, migrator, database and admin UI.
3. Open the links in the AppHost terminal output.

If this is your first run, use a disposable data path during experiments:

```text
OPENIDENTITYSTACK_DISABLE_DATA_VOLUME=true
```

Set this to `true` if you want clean state between runs.

## Step 4: Production readiness checks

Before first production login, ensure:

- persistent data store is durable and backed up
- signing/encryption certificate strategy is defined
- admin bootstrap account can be created
- CORS and reverse-proxy headers are correctly configured
- secrets are never committed to source code

## Step 5: First user/admin account

Start with one service-owned admin account for onboarding:

1. deploy a minimal first set of roles and clients
2. create an admin user in the admin UI
3. test login and one token-flow end-to-end

Store credentials with your existing secret management process.

