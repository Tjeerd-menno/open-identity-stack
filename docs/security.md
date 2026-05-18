# Security overview

OpenIdentityStack can be run safely in production, but only when the deployment treats keys, browser origins, bootstrap access, and database continuity as core operational concerns.

## Start here

- [Hardening checklist](security/hardening-checklist.md)
- [Secrets management](security/secrets-management.md)
- [Certificates and keys](security/certificates-and-keys.md)
- [Backup and recovery](security/backup-and-recovery.md)

## Core principles

- keep secrets and keys out of source control
- use dedicated signing and encryption material in production
- scope browser origins and forwarded headers deliberately
- protect the first admin bootstrap flow
- test backup and restore before relying on the deployment
