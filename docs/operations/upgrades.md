# Upgrades

Treat upgrades as coordinated changes across the migrator, API, admin web, and deployment configuration.

## Recommended sequence

1. review release notes and image tags
2. back up PostgreSQL
3. update manifests or runtime configuration
4. run the database migrator
5. roll out the API
6. roll out the admin web
7. verify health, sign-in, and at least one token flow

## Pay special attention to

- redirect URI changes for admin web or integrated applications
- certificate or key location changes
- browser origin changes
- changes to the admin seed behavior

## Rollback posture

Before upgrading, decide:

- whether the database migration is reversible
- which image tags you can redeploy quickly
- how to validate that older clients still trust the authority metadata after rollback
