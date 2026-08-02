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
- Request Objects (JAR) — see below

## Request Objects (JAR)

Request objects are **not supported**. An authorization request carrying a `request`
parameter is rejected with `error=request_not_supported`, redirected to the client's
validated `redirect_uri` as OpenID Connect Core §3.1.2.6 prescribes. Rejection is an
explicitly permitted posture for the Basic OP profile: the conformance suite accepts
`request_not_supported` in `oidcc-ensure-request-object-with-redirect-uri`, and
`oidcc-unsigned-request-object-supported-correctly-or-rejected-as-unsupported` skips
when the capability is not advertised.

Discovery is therefore **deliberately silent** on
`request_object_signing_alg_values_supported`. Advertising a capability the OP does not
implement is a worse failure than advertising nothing: relying parties would build
against a signalled feature that never works, and discovery metadata is precisely what
the Config OP plan certifies. OpenIddict 7.6 provides no server-side request-object
support to enable, so implementing JAR would mean hand-writing the parse, signature
verification, `iss`/`aud` binding, algorithm-confusion and replay defences — work that
belongs with the deferred FAPI profile, not with Basic OP certification.

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
