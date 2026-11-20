# Windows service deployment

Use this path only when your environment requires a Windows-hosted API instead of the container deployment model.

## What is packaged

Release artifacts include:

- published API binaries for `win-x64`
- PowerShell install and uninstall scripts from `deploy/windows-service`

## Installation flow

1. Install the x64 ASP.NET Core shared runtime for the release (.NET 11 for 1.0.0-rc.1) where the service can find it. Download the Windows ZIP and its adjacent SHA-256 file from the same release. Compare the output of `Get-FileHash -Algorithm SHA256` with the published checksum before extraction.
2. Prepare PostgreSQL. Run the **matching published DbMigrator** against that database and wait for a successful exit. The Windows ZIP packages only the API and service scripts. The migration job can run from a separate Linux container host with database access. Give it the application secret encryption key (`Secrets__EncryptionKey`), the separate OpenIddict signing and encryption certificates with private keys, the initial user, Management Web redirect URIs and explicit administrative bootstrap. Keep the secret values in the deployment secret store.
3. Extract the ZIP into a permanent directory on the Windows host. In `publish/appsettings.Production.json` or service-specific configuration, set the database connection, application encryption key, canonical HTTPS issuer, signing and encryption certificates, and the operator login listener. Obtain the Emergency Administrator ID from the bootstrap audit, set its certificate pin, and restrict access to configuration and private keys. [Administrator MFA](../security/administrator-mfa.md)
4. In elevated PowerShell, run the packaged `install-open-identity-stack.ps1`. It registers an automatic service and sets the machine-wide `ASPNETCORE_URLS`. Configure the intended Windows service identity and its file, key and database access before starting it.
5. Start `OpenIdentityStack`. Check readiness, discovery, a Management Web sign-in with PKCE, and an authorized Admin API operation.

For the RC1 Windows-only rehearsal, `scripts/rehearsal/windows-install.ps1` retrieves the companion DbMigrator image by its published digest. It verifies the index, linux/amd64 manifest and application layer, extracts the framework-dependent `/app` payload, and runs `dotnet OpenIdentityStack.DbMigrator.dll` on Windows. Keep the complete payload together. The ZIP's service installer operates on the API executable after migration.

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
