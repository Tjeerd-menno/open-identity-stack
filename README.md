# OpenIdentityStack

[![CI](https://github.com/Tjeerd-menno/open-identity-stack/actions/workflows/ci.yml/badge.svg)](https://github.com/Tjeerd-menno/open-identity-stack/actions/workflows/ci.yml)
[![Docs](https://img.shields.io/badge/docs-openidentitystack-blue)](https://tjeerd-menno.github.io/open-identity-stack/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

OpenIdentityStack is an OpenIddict-based identity and access management stack built with .NET 10, .NET Aspire, PostgreSQL, and React.

## Features

- OpenID Connect/OAuth 2.0 authorization server using OpenIddict.
- Authorization Code with PKCE, Client Credentials, and Refresh Token flows.
- Local email/password authentication with secure password hashing.
- External OIDC/OAuth2 federation providers.
- Role-based access control with groups, permissions, and delegated administration.
- Service accounts, client management, certificate support, and secret rotation.
- User sessions, session revocation, upstream identities, and logout endpoints.
- React/Vite admin web UI.
- Dedicated database migrator for schema updates and development seeding.

## Documentation

User documentation now lives at:

- [OpenIdentityStack Docs](https://tjeerd-menno.github.io/open-identity-stack/)

Use this site for installation, quick start, and operations guidance.
Developers can still find implementation and engineering notes in the repo's `docs/reference` section.

## Repository layout

```text
open-identity-stack/
├── OpenIdentityStack.slnx
├── src/
│   ├── SharedKernel/
│   ├── OpenIdentityStack.AppHost/
│   ├── OpenIdentityStack.ServiceDefaults/
│   ├── OpenIdentityStack.Api/
│   ├── OpenIdentityStack.Application/
│   ├── OpenIdentityStack.Domain/
│   ├── OpenIdentityStack.Infrastructure/
│   ├── OpenIdentityStack.DbMigrator/
│   └── OpenIdentityStack.AdminWeb/
├── tests/
├── deploy/
│   ├── open-identity-stack/
│   └── windows-service/
├── docs/
└── specs/
```

## Quick start

```bash
git clone https://github.com/Tjeerd-menno/open-identity-stack.git
cd open-identity-stack

dotnet restore OpenIdentityStack.slnx
dotnet build OpenIdentityStack.slnx --no-restore
```

Run the Aspire app model:

```bash
cd src/OpenIdentityStack.AppHost
dotnet run
```

The AppHost starts PostgreSQL, runs the OpenIdentityStack migrator, starts the API, and starts the admin web UI. In normal local development it uses a persistent PostgreSQL data volume; set `OPENIDENTITYSTACK_DISABLE_DATA_VOLUME=true` for disposable test-style runs.

## Build and test

```bash
dotnet build OpenIdentityStack.slnx

dotnet test --project tests/OpenIdentityStack.Domain.Tests/OpenIdentityStack.Domain.Tests.csproj
dotnet test --project tests/OpenIdentityStack.Application.Tests/OpenIdentityStack.Application.Tests.csproj
dotnet test --project tests/OpenIdentityStack.Infrastructure.Tests/OpenIdentityStack.Infrastructure.Tests.csproj
dotnet test --project tests/OpenIdentityStack.Api.UnitTests/OpenIdentityStack.Api.UnitTests.csproj
```

## Admin web

```bash
cd src/OpenIdentityStack.AdminWeb
npm install
npm run build
npm run lint
npm test
```

## Release artifacts

The release workflow is designed to publish:

- `ghcr.io/tjeerd-menno/open-identity-stack-api:<version>`
- `ghcr.io/tjeerd-menno/open-identity-stack-db-migrator:<version>`
- `ghcr.io/tjeerd-menno/open-identity-stack-admin-web:<version>`
- `open-identity-stack-api-win-x64-<version>.zip`

The Windows service zip includes the published API executable and PowerShell install/uninstall scripts from `deploy/windows-service`.

See [GHCR publishing](docs/GHCR-PUBLISHING.md) for image names, tags, triggers, and required workflow permissions.

## Configuration

| Variable | Description | Default |
| --- | --- | --- |
| `ConnectionStrings__openidentitystack` | PostgreSQL connection string | Aspire-managed locally |
| `OPENIDENTITYSTACK_DISABLE_DATA_VOLUME` | Disable persistent Aspire PostgreSQL volume when set to `true` | `false` |
| `ForwardedHeaders__Enabled` | Enables forwarded header processing behind a reverse proxy | `false` |
| `AllowedCorsOrigins` | Comma-separated production CORS origins | unset |
| `VITE_OIDC_AUTHORITY` | Admin web OIDC authority | AppHost-provided locally |
| `VITE_API_BASE_URL` | Admin web API base URL | AppHost-provided locally |

Production deployments must provide persistent data-protection storage and OpenIddict signing/encryption certificates. Development and Testing environments may use ephemeral development certificates.

## License

OpenIdentityStack is licensed under the [MIT License](LICENSE).
