# Runtime settings

These settings matter most when moving from local experiments to a durable deployment.

## Database

`ConnectionStrings__openidentitystack`

Provides the PostgreSQL connection string used by the API and database migrator.

## Forwarded headers

`ForwardedHeaders__Enabled`

Enables forwarded header handling when the API is behind a reverse proxy or ingress.

`ForwardedHeaders__KnownProxies__*`

Use these values to trust specific proxy IPs.

`ForwardedHeaders__KnownNetworks__*`

Use these values to trust specific proxy networks in CIDR format.

## Browser origins

`AllowedCorsOrigins`

Comma-separated list of allowed browser origins for the admin web in production. In local development and testing, the API allows dynamic origins for Aspire and test-hosted flows.

## Certificates and keys

The API and migrator consume certificate and private key file paths for:

- OpenIddict signing
- OpenIddict encryption

For Kubernetes deployments, a common convention is to project PEM material into `/var/run/openiddict-certs`.

## Local composition toggles

`OPENIDENTITYSTACK_DISABLE_DATA_VOLUME`

Disables the persistent PostgreSQL volume in the Aspire local composition.

`OPENIDENTITYSTACK_ENABLE_ADMINWEB`

Lets you skip the admin web app in local composition when you only want the backend runtime.
