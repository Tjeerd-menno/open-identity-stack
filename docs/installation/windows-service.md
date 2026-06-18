# Windows service deployment

Use this path only when your environment requires a Windows-hosted API instead of the container deployment model.

## What is packaged

Release artifacts include:

- published API binaries for `win-x64`
- PowerShell install and uninstall scripts from `deploy/windows-service`

## Installation flow

1. download the Windows service zip from a release
2. extract it on the target host
3. run the provided install script with the intended service account
4. provide configuration for database connectivity, proxy behavior, and certificates
5. start the service and verify health

## Runtime considerations

A Windows service deployment still needs the same production inputs as the container path:

- PostgreSQL connection string
- OpenIddict signing and encryption material
- management web hosting strategy or reverse proxy plan
- log collection and service recovery policy

## Validation checklist

Confirm:

1. the service starts cleanly
2. `/health` and `/alive` respond through the host or reverse proxy path
3. token issuance works with the configured certificates
4. the management web can still reach the API authority you published

If your environment supports containers, the Kubernetes or container path is usually easier to operate and upgrade.
