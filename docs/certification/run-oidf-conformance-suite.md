# Running the OpenID Foundation Conformance Suite

## Purpose

This document explains how to run the OpenID Foundation conformance tests against OpenIdentityStack.

## Test Target

OpenIdentityStack certification environment:

```text
https://<public-issuer-host>/
```

## Discovery URL

```text
https://<public-issuer-host>/.well-known/openid-configuration
```

## Initial Test Plan

Use the hosted OpenID Foundation conformance suite and select:

```text
OpenID Connect Core: Basic Certification Profile Authorization server test
```

Use a test plan in the "Test an OpenID Provider" section.

## Client Registration Mode

Use static client registration for the first certification milestone.

Seeded clients:

```text
oidf-code-client
oidf-code-client-post
oidf-code-client-takeover
```

The initial milestone does not advertise or certify Dynamic Client Registration, Implicit, Hybrid, Logout, FAPI, Federation, or eKYC profiles.

## Required Preparation

- Deploy the dedicated certification environment using your private operational deployment assets.
- Seed certification users and clients with `OPENIDENTITYSTACK_SEED_PROFILE=certification`.
- Configure the OpenID Foundation alias in `Seed__Certification__Alias`.
- Verify the public discovery document.
- Verify the JWKS endpoint.
- Verify login works for the certification users.
- Register the OpenID Foundation redirect URI for the chosen alias:

```text
https://www.certification.openid.net/test/a/<ALIAS>/callback
```

When using the staging conformance suite, also register:

```text
https://staging.certification.openid.net/test/a/<ALIAS>/callback
```

## Suggested Suite Configuration

- Server metadata discovery URL: `https://<public-issuer-host>/.well-known/openid-configuration`
- Client authentication method for `oidf-code-client`: `client_secret_basic`
- Client authentication method for `oidf-code-client-post`: `client_secret_post`
- Use PKCE where the suite asks for it.
- Use the dedicated certification test users only.

## Result Handling

- All required tests must finish successfully.
- `FAILED` or `INTERRUPTED` results block certification.
- Review every warning and document why it is acceptable before submission.
- Export result ZIP files after the final run.
- Do not submit or archive logs containing reusable secrets.
- Deactivate or rotate certification user passwords and client secrets after exporting final evidence.
