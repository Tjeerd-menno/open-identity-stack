# Startup failures

## Symptoms

- AppHost never settles
- the API exits immediately
- readiness never becomes healthy

## Likely causes

- .NET or Node prerequisites are missing in local environments
- PostgreSQL is not reachable
- the migrator failed before the API became ready
- ports are already in use
- a Native AOT package was built without the required platform toolchain or without rerunning the migrator first

## Checks

1. inspect AppHost, API, and migrator logs
2. verify PostgreSQL connectivity
3. confirm `/health` and `/alive` behavior
4. confirm no conflicting local ports are already bound

## Fixes

- correct prerequisites and retry
- use `OPENIDENTITYSTACK_DISABLE_DATA_VOLUME=true` for a clean local run when state is corrupted
- repair the database connection string or secret reference
- fix container startup ordering so the migrator finishes first
- rebuild Native AOT artifacts on a host with the platform linker and C++ toolchain installed

## When to escalate

Escalate when startup consistently fails after database and certificate inputs are confirmed.
