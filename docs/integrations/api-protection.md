# API protection

Applications that accept OpenIdentityStack access tokens should validate them against the authority metadata and signing keys published by the platform.

## Minimum validation points

- issuer
- audience
- signature
- token lifetime
- any application-specific scopes or claims

## Operational guidance

- keep your API clock synchronized
- consume JWKS and discovery metadata from the public authority
- test validation again whenever certificates rotate

## Concrete example

Fetch the discovery document:

```bash
curl https://identity.example.com/.well-known/openid-configuration
```

Fetch the current signing keys:

```bash
curl https://identity.example.com/.well-known/jwks
```

Your protected API should validate that:

- `iss` matches `https://identity.example.com`
- the expected audience is present
- the signing key matches one of the published JWKS entries

## Common mistakes

- using the wrong issuer URL
- validating against stale keys
- mixing production and non-production authorities
