# OpenIdentityStack Documentation

OpenIdentityStack is an OAuth 2.0 and OpenID Connect identity platform for teams that need a self-hosted place to manage sign-in, client registration, delegated administration, and service-to-service access.

This site is for people deploying, operating, and integrating the product:

- platform teams
- application operators
- administrators
- architects evaluating adoption
- application teams integrating against the authority

Implementation notes, certification work, and engineering guides stay under [Reference](reference/index.md).

## Choose your path

### I am evaluating OpenIdentityStack

Start with:

- [Getting started overview](getting-started/overview.md)
- [Features overview](features/overview.md)
- [Security overview](security.md)

### I want to run it locally

Start with:

- [Quick start](getting-started/quick-start.md)
- [Local installation](installation/local.md)
- [Configuration overview](configuration/index.md)

### I want to deploy it to production

Start with:

- [Guided setup](getting-started/guided-setup.md)
- [Production deployment](installation/production.md)
- [Operations overview](operations/index.md)
- [Hardening checklist](security/hardening-checklist.md)

### I want to operate an existing deployment

Start with:

- [Operations overview](operations/index.md)
- [Health and observability](operations/health-and-observability.md)
- [Database operations](operations/database-operations.md)
- [Troubleshooting overview](troubleshooting.md)

### I want to integrate an application

Start with:

- [Integration overview](integrations/index.md)
- [Web app OIDC integration](integrations/web-app-oidc.md)
- [API protection](integrations/api-protection.md)
- [Machine-to-machine access](integrations/machine-to-machine.md)

## What this documentation covers

- product capabilities and deployment fit
- local evaluation and first-run guidance
- Kubernetes, container, and Windows service deployment paths
- configuration and runtime operations
- administrator workflows for users, roles, clients, and service accounts
- troubleshooting and production hardening

## Contributing to docs

To preview documentation locally:

```bash
python -m pip install -r docs/requirements.txt
mkdocs serve
```

When you add or change product behavior, update the user-facing path that an operator or administrator would actually follow.
