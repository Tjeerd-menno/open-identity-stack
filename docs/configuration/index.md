# Configuration overview

OpenIdentityStack configuration falls into four areas:

- database connectivity
- browser and reverse proxy behavior
- OpenIddict certificates and client bootstrap
- local composition toggles for Aspire

## Configuration sources

Depending on your deployment path, configuration may come from:

- environment variables
- appsettings files
- Kubernetes secrets and config maps
- Aspire resource wiring

## Start here

- [Runtime settings](runtime-settings.md)
- [Environment variables](environment-variables.md)

## Practical advice

Keep production configuration external to the application image. Treat certificates, connection strings, and admin seed values as deployment-time inputs owned by your platform or security process.
