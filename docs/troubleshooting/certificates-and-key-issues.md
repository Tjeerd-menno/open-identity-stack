# Certificates and key issues

## Symptoms

- API startup fails while loading OpenIddict material
- token issuance fails
- the migrator fails in production but local development works

## Likely causes

- certificate path is wrong
- private key file is missing
- projected secret name is wrong
- rotated material was published inconsistently across components

## Checks

1. confirm the configured certificate and key paths exist
2. confirm the Kubernetes secret names match the manifests
3. confirm both API and migrator see the same signing and encryption material
4. confirm consumers trust the newly published keys

## Fixes

- repair mounted file paths
- redeploy the projected secret
- coordinate rotation between migrator, API, and clients

## When to escalate

Escalate when rotation or PEM parsing fails after file presence and secret names are confirmed.
