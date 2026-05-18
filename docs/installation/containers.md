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

## What to keep out of the image

- production secrets
- private keys checked into source control
- environment-specific hostnames

Use your deployment platform to inject secrets, config maps, and mounted files instead.
