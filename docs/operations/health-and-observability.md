# Health and observability

OpenIdentityStack exposes simple operational signals that should be wired into your platform checks.

## Health endpoints

The bundled deployment uses:

- `/health` for readiness
- `/alive` for liveness

The Kubernetes manifests point readiness and liveness probes at those endpoints for the API deployment.

## What to watch during startup

Look for:

- successful database connectivity
- completion of the database migrator
- clean API startup without certificate loading errors
- successful management web availability

## Logs that matter first

When triaging a fresh problem, start with:

- database connection failures
- OpenIddict certificate loading errors
- forwarded header validation issues
- CORS failures between the management web and API
- login and token endpoint failures

## Operational behaviors worth knowing

- the API applies rate limiting to interactive login and token endpoints
- HSTS and HTTPS redirection are enabled outside development and testing
- data-protection keys are persisted to the database

This means database health is part of both application state and crypto continuity.
