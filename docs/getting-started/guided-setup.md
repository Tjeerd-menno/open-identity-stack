# Guided setup

Use this path when you want to choose the right deployment model and avoid missing the setup decisions that matter later.

## Step 1: Choose your deployment style

### Local evaluation

Use this path when you want to:

- explore the admin UI
- verify local sign-in flows
- integrate a development application
- work with disposable or semi-persistent PostgreSQL state

Go to [Local installation](../installation/local.md).

### Production deployment

Use this path when you need:

- durable PostgreSQL storage
- real signing and encryption keys
- ingress or reverse proxy handling
- operational readiness, backups, and rollout discipline

Go to [Production deployment](../installation/production.md).

### Windows service deployment

Use this only when your environment requires a Windows-hosted API service and you do not want to run the container path.

Go to [Windows service deployment](../installation/windows-service.md).

## Step 2: Confirm prerequisites

### Local

- .NET 10 SDK
- Node.js LTS
- Git

### Production

- Kubernetes cluster or equivalent container host
- PostgreSQL connectivity
- secret management process
- certificate and key ownership model
- ingress or reverse proxy plan

## Step 3: Decide how keys and certificates are managed

Before production, decide:

- where OpenIddict signing material lives
- where OpenIddict encryption material lives
- how certificates are renewed and rolled out
- how applications validate issuer and JWKS metadata after rotation

The bundled Kubernetes path expects mounted PEM files projected from certificate secrets. See [Certificates and keys](../security/certificates-and-keys.md).

## Step 4: Plan the first bootstrap

For a clean first rollout, plan these items together:

- database connection string
- initial admin identity
- admin web redirect URIs
- allowed browser origins
- first client application
- first service account, if machine-to-machine access is part of the rollout

The Kubernetes manifests support an optional admin seed secret for first-run onboarding.

## Step 5: Validate the first successful flow

Your first validation should prove all of these:

1. the API is healthy
2. the admin web can reach the API
3. the first admin can sign in
4. at least one client can complete its intended flow
5. logs and health endpoints are visible to operators

From here, continue with [Quick start](quick-start.md) for local usage or [Operations overview](../operations/index.md) for production rollout preparation.
