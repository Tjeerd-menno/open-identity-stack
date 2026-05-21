# Database operations

OpenIdentityStack uses PostgreSQL for application data and persisted data-protection keys.

## Migration model

Schema updates are handled by the dedicated DbMigrator rather than by the API process itself. In the bundled Kubernetes deployment, the migrator runs as a job before the API rollout is considered complete.

Native AOT API deployments keep the same migration model. Do not run EF migration or schema creation APIs from the native API process; run `OpenIdentityStack.DbMigrator` or an approved migration bundle before starting the API artifact.

## Backup and restore expectations

Your database runbook should include:

- regular backups
- restore testing
- a plan for data-protection continuity
- rollback expectations when an application release is reverted

## First-run seeding

The current deployment paths support two different seed shapes:

- development seed data for local AppHost flows
- optional production admin seeding through `open-identity-stack-admin-seed`

Treat production seeding as a bootstrap event, not a permanent configuration source.

## Checks after a restore

After restoring a database:

1. verify the API can start
2. verify `/health`
3. verify sign-in works
4. verify at least one client application can obtain a token
5. verify data-protection-dependent flows still behave correctly
