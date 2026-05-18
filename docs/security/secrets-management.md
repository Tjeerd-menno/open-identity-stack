# Secrets management

Treat all of the following as secrets or sensitive deployment inputs:

- database connection strings
- OpenIddict private keys
- client secrets
- service account secrets
- admin bootstrap passwords

## Recommended approach

- inject secrets at deploy time
- keep them in a centralized secret manager or Kubernetes secret workflow
- avoid baking them into container images or appsettings files
- rotate secrets on an agreed schedule

## Extra care areas

- admin seed secrets should be removed or tightly controlled after bootstrap
- service account secrets should be owned by the workload team that uses them
- certificate files should be mounted from trusted platform resources
