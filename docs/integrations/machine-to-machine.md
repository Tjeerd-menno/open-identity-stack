# Machine-to-machine access

Use this path for background jobs, services, and daemons that need non-interactive access.

## Recommended flow

Client credentials is the primary machine-to-machine pattern supported by the current stack.

## What you need

- a service account or client record
- a secret or certificate, depending on your trust model
- the correct token endpoint
- downstream APIs that understand the issued tokens

## Concrete example

Request a token with client credentials:

```bash
curl -X POST https://identity.example.com/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=reporting-worker" \
  -d "client_secret=replace-with-real-secret"
```

The response should contain an `access_token`, `token_type`, and expiry information.

## Validation checklist

1. request a token
2. inspect the resulting claims
3. call the protected API
4. rotate the secret and verify the new credential works
