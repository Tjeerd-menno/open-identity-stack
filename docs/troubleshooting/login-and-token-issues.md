# Login and token issues

## Symptoms

- users cannot sign in
- token requests fail
- browser flows loop or return errors
- downstream APIs reject tokens

## Likely causes

- wrong redirect URI
- wrong issuer or audience
- stale signing keys on the consumer side
- missing admin web browser origin
- upstream federation mismatch

## Checks

1. confirm discovery metadata loads from the expected authority
2. confirm the client record uses the exact redirect URI
3. inspect token endpoint and login logs
4. verify that the consumer validates the current issuer and keys

## Fixes

- repair redirect URI and logout URI configuration
- correct the consumer issuer or audience
- refresh JWKS consumers after key rotation
- review provider-specific claims and callback configuration for federation

## When to escalate

Escalate when the flow fails after client settings, keys, and browser origins have all been verified.
