# Verify an integration

When an application is wired up for the first time, use this checklist before calling the integration done.

## Discovery and keys

- fetch the discovery document
- fetch the JWKS
- confirm the issuer matches the public authority URL

Example checks:

```bash
curl https://identity.example.com/.well-known/openid-configuration
curl https://identity.example.com/.well-known/jwks
```

## Interactive applications

- sign in successfully
- return to the exact redirect URI
- sign out successfully

## Machine-to-machine applications

- request a token
- validate the intended claims
- confirm the protected API accepts the token

Example token request:

```bash
curl -X POST https://identity.example.com/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=reporting-worker" \
  -d "client_secret=replace-with-real-secret"
```

## When something fails

Continue to:

- [Login and token issues](../troubleshooting/login-and-token-issues.md)
- [Clients and service accounts](../troubleshooting/clients-and-service-accounts.md)
