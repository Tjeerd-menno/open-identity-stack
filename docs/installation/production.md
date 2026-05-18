# Production deployment

Use this section for production planning and rollout.

## Deployment model

OpenIdentityStack has container images for:

- API
- admin web
- database migrator

## Kubernetes example

The repository includes a Kubernetes manifest set under `deploy/open-identity-stack`.

### Required secrets

Create database and application secrets in the namespace you deploy into before applying resources.

```text
kubectl create namespace open-identity-stack

kubectl create secret generic open-identity-stack-db-app \
  --namespace open-identity-stack \
  --from-literal=username=openidentitystack \
  --from-literal=password=<replace-with-strong-password> \
  --dry-run=client -o yaml | kubectl apply -f -

kubectl create secret generic open-identity-stack-app \
  --namespace open-identity-stack \
  --from-literal=connection-string="Host=open-identity-stack-db-rw;Port=5432;Database=openidentitystack;Username=openidentitystack;Password=<replace-with-password>;SSL Mode=Disable;Trust Server Certificate=true" \
  --dry-run=client -o yaml | kubectl apply -f -
```

### Production certificates

Production requires signing and encryption material for OpenIddict. Store those securely and mount them as
configured in your deployment manifests.

If your cluster is already using cert-manager, use dedicated certificate secrets for:

- OpenIddict signing certificate
- OpenIddict encryption certificate

### Apply and verify

```text
kubectl apply -k deploy/open-identity-stack
kubectl wait --for=condition=Ready cluster/open-identity-stack-db -n open-identity-stack --timeout=10m
kubectl wait --for=condition=complete job/open-identity-stack-db-migrator -n open-identity-stack --timeout=10m
kubectl rollout status deployment/open-identity-stack-api -n open-identity-stack --timeout=10m
kubectl rollout status deployment/open-identity-stack-adminweb -n open-identity-stack --timeout=10m
```

### Image location note

These manifests reference container images in `deploy/open-identity-stack` and may reference image registries
outside your environment. Update registry names and credentials to match your GitHub Container Registry
or internal registry strategy.

## Optional production seed

For initial onboarding, create an admin seed secret according to your secret management policy
and use it to create an initial super-admin user.

