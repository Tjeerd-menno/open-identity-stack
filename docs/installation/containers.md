# Container installation

OpenIdentityStack publishes separate container images for:

- the API
- the admin web
- the database migrator

## What each image is for

### API

Runs the OpenID Connect and OAuth 2.0 authority, admin APIs, login UI, and health endpoints.

### Admin web

Runs the browser-based administration experience.

### Database migrator

Runs schema updates and optional first-run seeding before the API starts serving traffic.

## Required configuration

At a minimum, a container deployment needs:

- `ConnectionStrings__openidentitystack`
- certificate paths or mounted certificate files for OpenIddict signing and encryption
- `AllowedCorsOrigins` for browser-based admin traffic
- public URLs and reverse proxy behavior that match how integrators will discover the authority

## Recommended rollout shape

1. run the database migrator first
2. start the API and wait for `/health`
3. start the admin web
4. verify sign-in and a first client flow

## Native AOT publish

The API service has opt-in Native AOT publish profiles for local validation:

```powershell
dotnet publish src\OpenIdentityStack.Api\OpenIdentityStack.Api.csproj -c Release -r linux-x64 -p:IsAotPublish=true -p:PublishAot=true
dotnet publish src\OpenIdentityStack.Api\OpenIdentityStack.Api.csproj -c Release -r win-x64 -p:IsAotPublish=true -p:PublishAot=true
```

The AOT container target uses `linux-x64` and the `runtime-deps` base image. The default release image remains the framework-dependent API image until the MVC/Razor OIDC endpoints are fully moved to AOT-compatible Minimal API handlers. The database migrator remains a separate non-AOT process and must finish before the API starts.

The current native profile excludes MVC and Razor endpoint registration because those ASP.NET Core components are not Native AOT compatible. Keep `/connect/*` and `/Account/*` sign-in flow validation in the release checklist until those endpoints are fully moved to AOT-compatible Minimal API handlers.

Podman build example:

```powershell
podman build --network host --target native-aot -f src\OpenIdentityStack.Api\Dockerfile -t openidentitystack-api:aot .
```

## What to keep out of the image

- production secrets
- private keys checked into source control
- environment-specific hostnames

Use your deployment platform to inject secrets, config maps, and mounted files instead.
