# Production deployment

Use this guide for production planning and rollout. The repository includes a Kubernetes deployment example and container images for the API, admin web, and database migrator.

## Deployment model

Production deployments should treat these as separate concerns:

- API runtime
- admin web runtime
- database migrator job
- PostgreSQL
- signing and encryption certificate delivery
- ingress, DNS, and browser origin policy

## Kubernetes path

The repository includes a kustomize deployment under `deploy/open-identity-stack` with:

- an `open-identity-stack` namespace
- a CNPG PostgreSQL cluster
- a database migrator job
- API and admin web deployments
- cert-manager resources for OpenIddict signing and encryption certificates

### Create required secrets

At minimum, create:

- `open-identity-stack-db-app`
- `open-identity-stack-app`

The included deployment README also documents an optional `open-identity-stack-admin-seed` secret for first-run admin bootstrapping.

### Apply and verify

```text
kubectl apply -k deploy/open-identity-stack
kubectl wait --for=condition=Ready cluster/open-identity-stack-db -n open-identity-stack --timeout=10m
kubectl wait --for=condition=complete job/open-identity-stack-db-migrator -n open-identity-stack --timeout=10m
kubectl rollout status deployment/open-identity-stack-api -n open-identity-stack --timeout=10m
kubectl rollout status deployment/open-identity-stack-adminweb -n open-identity-stack --timeout=10m
```

## Container responsibilities

### API

The API deployment expects:

- `ConnectionStrings__openidentitystack`
- `AllowedCorsOrigins`
- mounted certificate and private key files for signing and encryption

### Admin web

The admin web deployment reads its browser-facing configuration from the shared config map. It should point at the public authority and API paths that your users will actually reach.

### Database migrator

The migrator runs schema updates and can seed:

- admin web client redirect URIs
- an initial admin account through the optional seed secret

## Production readiness checklist

Before first login, confirm:

1. certificate and key rotation ownership is defined
2. backup and restore for PostgreSQL is in place
3. admin bootstrap credentials are protected
4. browser origins are explicitly configured
5. health endpoints and logs are wired into operations tooling

## Related guides

- [Container installation notes](containers.md)
- [Configuration overview](../configuration/index.md)
- [Operations overview](../operations/index.md)
- [Hardening checklist](../security/hardening-checklist.md)
