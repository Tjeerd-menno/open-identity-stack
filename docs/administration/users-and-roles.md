# Users and roles

OpenIdentityStack separates identity records from administrative permissions so teams can manage user lifecycle and platform access cleanly.

## Common tasks

- create or disable a user
- reset a password
- assign roles
- review effective permissions

## Role design guidance

Start with a small set of platform roles such as:

- full platform administrator
- user administrator
- client administrator
- service account administrator
- read-only auditor

Keep day-to-day operator roles narrower than `super-admin`.

## Permissions you can model

The product currently defines permission families for:

- users
- roles
- groups
- service accounts
- service permissions
- sessions
- providers
- clients
- audit logs
- system settings and metrics

Document which roles your organization uses for each operational duty before broadening access.
