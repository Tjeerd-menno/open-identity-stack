# Clients and service accounts

## Symptoms

- a client cannot complete sign-in
- client credentials requests fail
- rotated secrets stop working unexpectedly

## Likely causes

- wrong client identifier or secret
- wrong grant type for the integration
- redirect URIs do not match exactly
- service account permissions are too broad or too narrow

## Checks

1. review the client or service account configuration
2. confirm current secret or certificate material
3. confirm redirect and post-logout URIs
4. verify the intended permissions on the workload

## Fixes

- rotate and redistribute credentials
- repair redirect URI configuration
- split shared service accounts into workload-specific identities

## When to escalate

Escalate when credential and grant settings are correct but the authority still rejects the client flow.
