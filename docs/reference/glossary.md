# Glossary

## Authority

The OpenIdentityStack deployment that signs users in, issues tokens, and publishes identity metadata.

## Client

An application registered to use OpenIdentityStack for sign-in or token issuance.

## Redirect URI

The exact application URL that receives the browser after a successful sign-in flow.

## Post-logout redirect URI

The application URL that receives the browser after logout completes.

## Service account

A non-interactive identity used by workloads, daemons, or backend services.

## Signing certificate

The certificate and private key used to sign issued tokens.

## Encryption certificate

The certificate and private key used when token encryption is enabled.

## Discovery document

The OpenID Connect metadata document published at `/.well-known/openid-configuration`.

## JWKS

The JSON Web Key Set published by the authority so clients and APIs can validate tokens.

## Introspection

A protocol endpoint that lets trusted callers ask the authority whether a token is still valid and what it represents.

## Revocation

A protocol endpoint used to invalidate a token before it would naturally expire.
