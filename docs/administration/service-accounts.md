# Service accounts

Service accounts are for non-interactive workloads that need client credentials or certificate-backed access.

## Common tasks

- create the service account
- grant the minimum required permissions
- rotate the client secret
- attach or replace certificates when your trust model requires them
- disable or delete unused credentials

## Good operating habits

- separate service accounts by workload, not by team convenience
- grant only the service permissions each workload needs
- rotate secrets on a predictable schedule
- disable dormant accounts instead of leaving them permanently active

## Validation

After creating or rotating credentials:

1. request a token
2. call the intended downstream API
3. confirm the old credential no longer works when rotation is complete
