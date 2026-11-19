# Backup and recovery

Security is not only about prevention. For an identity platform, recovery matters just as much.

## Protect these assets

- PostgreSQL data
- persisted data-protection keys in the database
- deployment secrets and certificate sources
- the documented bootstrap and recovery process

## Recovery checklist

1. Record the running image digests or Windows ZIP checksum and canonical issuer. Take a consistent PostgreSQL backup including the Data Protection key table. Securely back up the corresponding application encryption key, signing and encryption private keys, TLS and MFA configuration, and database access configuration. Keep backup access under operator control.
2. Restore to an empty database on an isolated deployment with the pre-upgrade artifacts. Map the canonical issuer to the isolated deployment for rehearsal clients. Keep public traffic drained. Verify the restored Data Protection key records and restore the matching deployment secrets and certificates before starting the API.
3. Verify readiness and compare discovery's issuer and signing key with the expected values. Sign in using the recovered administrator, complete PKCE and refresh, and perform an authorized Admin API read. Obtain a client-credentials token using an existing client secret. Check the expected permissions, registration IDs, and grants.
4. Before returning a restored installation to service, reconcile account disablements, withdrawn grants and session revocations made after the backup. Obtain the recovery owner's approval. If the upgraded schema has already been applied, keep traffic closed while repairing forward or restore the complete pre-upgrade set. [Upgrade sequence](../operations/upgrades.md)

Recovery is only complete when applications can trust and use the authority again.
