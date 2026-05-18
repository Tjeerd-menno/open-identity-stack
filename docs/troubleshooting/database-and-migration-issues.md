# Database and migration issues

## Symptoms

- the migrator job never completes
- the API starts but fails health checks shortly after
- schema-dependent features fail after deployment

## Likely causes

- wrong connection string
- missing database secret
- PostgreSQL cluster not ready
- migration failure during startup rollout

## Checks

1. inspect the DbMigrator logs
2. verify `ConnectionStrings__openidentitystack`
3. confirm the database cluster is ready
4. confirm the expected schema exists after migration

## Fixes

- correct the connection string secret
- rerun the migrator after the database is healthy
- restore from backup if a failed release left the database in a bad state

## When to escalate

Escalate when the migrator fails with application exceptions or data anomalies rather than simple connectivity problems.
