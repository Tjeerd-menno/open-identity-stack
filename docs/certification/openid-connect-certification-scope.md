# OpenID Connect Certification Scope

## Product

OpenIdentityStack

## Certification Type

OpenID Provider

## Initial Certification Profiles

- OpenID Connect Core Basic OP
- Discovery-based OP configuration
- Authorization Code Flow
- PKCE
- Static client registration

## Deferred Profiles

- Dynamic Client Registration
- Implicit Flow
- Hybrid Flow
- Logout profiles
- FAPI
- OpenID Federation
- eKYC / Identity Assurance

## Certification Environment

The certification environment is a dedicated public deployment of OpenIdentityStack used only for OpenID Foundation conformance testing.

The public issuer URL is intentionally environment-specific and is managed in private operational deployment assets outside this repository.

```text
https://<public-issuer-host>/
```

## Certification Claim

The project must not display the OpenID Certified mark until certification has been approved by the OpenID Foundation.

Until then, use this wording:

```text
OpenIdentityStack is designed to implement OpenID Connect using OpenIddict and is being prepared for OpenID Provider certification.
```
