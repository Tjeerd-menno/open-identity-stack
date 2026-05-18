# Backup and recovery

Security is not only about prevention. For an identity platform, recovery matters just as much.

## Protect these assets

- PostgreSQL data
- persisted data-protection keys in the database
- deployment secrets and certificate sources
- the documented bootstrap and recovery process

## Recovery checklist

1. restore PostgreSQL
2. redeploy the API and migrator with the correct secrets and certificates
3. verify `/health`
4. verify admin sign-in
5. verify at least one client flow and one machine-to-machine flow

Recovery is only complete when applications can trust and use the authority again.
