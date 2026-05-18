# Certificates and keys

Production token issuance depends on stable signing and encryption material.

## What the platform needs

- a signing certificate and private key
- an encryption certificate and private key
- a rotation process that keeps clients and the authority in sync

## Current deployment pattern

The Kubernetes manifests project cert-manager secrets into the API and DbMigrator as PEM files for both signing and encryption.

## Rotation guidance

1. publish the new material through the deployment platform
2. confirm the authority exposes the expected metadata and keys
3. verify a token flow end to end
4. confirm downstream consumers accept the rotated keys

Do not rotate keys without validating the integrator side of the trust chain.
