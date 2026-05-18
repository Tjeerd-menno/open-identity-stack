# Federation providers

OpenIdentityStack supports external identity provider federation so you can connect upstream login systems without giving up control of your own authority.

## When to use federation

- workforce users sign in with an enterprise identity provider
- you need to preserve external identity context
- local admin and service account flows still need to coexist with external login

## Rollout guidance

1. start with one provider
2. validate claims and account matching rules
3. confirm sign-in and logout behavior
4. document which users should use local accounts versus external identities

## Common failure points

- incorrect redirect URIs
- missing claims needed for account mapping
- logout expectations that do not match the upstream provider
