# Production deployment

Use this guide for production planning and rollout. The repository provides container images for the API, management web, and database migrator.

## Deployment model

Production deployments should treat these as separate concerns:

- API runtime
- management web runtime
- database migrator job
- PostgreSQL
- signing and encryption certificate delivery
- ingress, DNS, and browser origin policy

## Kubernetes deployment

For Kubernetes deployments, you'll need to create manifests that include:

- an `open-identity-stack` namespace
- a PostgreSQL cluster (e.g., using CNPG or another operator)
- a database migrator job
- API and management web deployments
- certificate resources for OpenIddict signing and encryption (e.g., using cert-manager)

### Required secrets

At minimum, create:

- `open-identity-stack-db-app` - PostgreSQL connection string
- `open-identity-stack-app` - Application secrets

You may also want an `open-identity-stack-admin-seed` secret for first-run admin bootstrapping.

### Example deployment flow

```text
kubectl apply -k <your-kustomize-directory>
kubectl wait --for=condition=Ready <your-postgres-resource> -n open-identity-stack --timeout=10m
kubectl wait --for=condition=complete job/open-identity-stack-db-migrator -n open-identity-stack --timeout=10m
kubectl rollout status deployment/open-identity-stack-api -n open-identity-stack --timeout=10m
kubectl rollout status deployment/open-identity-stack-managementweb -n open-identity-stack --timeout=10m
```

## Container responsibilities

### API

The API deployment expects:

- `ConnectionStrings__openidentitystack`
- `AllowedCorsOrigins`
- mounted certificate and private key files for signing and encryption

### Management Web

The management web deployment reads its browser-facing configuration from the shared config map. It should point at the public authority and API paths that your users will actually reach.

### Database migrator

The migrator runs schema updates and can seed:

- management web client redirect URIs
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


