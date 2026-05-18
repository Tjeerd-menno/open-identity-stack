# Groups and delegation

Groups give you a cleaner way to assign identity context and delegated administration than attaching every permission directly to individual users.

## Typical uses

- map users into operational teams
- assign shared access boundaries
- grant a narrow set of admin permissions to a support or platform subgroup
- model tenant-aware or service-aware administration

## Recommended workflow

1. create the group
2. add members
3. define the roles or permission mappings the group should carry
4. validate effective access with a non-admin test account

## What to avoid

- groups that duplicate every role one-for-one
- groups whose purpose is unclear to future operators
- unreviewed inheritance that slowly becomes global access
