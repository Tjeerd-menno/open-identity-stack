# Configuration reference

This page is the compact reference view of the deployment inputs operators usually need at hand.

## Core runtime settings

| Setting | Meaning | Where it matters |
| --- | --- | --- |
| `ConnectionStrings__openidentitystack` | PostgreSQL connection string | API and DbMigrator |
| `AllowedCorsOrigins` | Comma-separated browser origins allowed to call the API | Production management web access |
| `ForwardedHeaders__Enabled` | Enables trusted proxy header processing | Ingress and reverse proxy deployments |
| `ForwardedHeaders__KnownProxies__*` | Trusted proxy IP addresses | Hardened proxy configuration |
| `ForwardedHeaders__KnownNetworks__*` | Trusted proxy CIDR ranges | Hardened proxy configuration |

## Certificate path settings

| Setting | Meaning |
| --- | --- |
| `OpenIddict__Certificates__Signing__CertificatePath` | PEM certificate used to sign tokens |
| `OpenIddict__Certificates__Signing__PrivateKeyPath` | Private key for token signing |
| `OpenIddict__Certificates__Encryption__CertificatePath` | PEM certificate used for token encryption |
| `OpenIddict__Certificates__Encryption__PrivateKeyPath` | Private key for token encryption |

## Local composition toggles

| Setting | Meaning |
| --- | --- |
| `OPENIDENTITYSTACK_DISABLE_DATA_VOLUME` | Disables persistent PostgreSQL state in the local AppHost |
| `OPENIDENTITYSTACK_ENABLE_MANAGEMENTWEB` | Disables the management web in the local AppHost when set to `false` |

## Seed settings

| Setting | Meaning |
| --- | --- |
| `Seed__DevelopmentData` | Enables the local development seed path |
| `Seed__AdminUser__Enabled` | Enables first-run admin seeding |
| `Seed__AdminUser__Email` | Email address of the seeded admin |
| `Seed__AdminUser__Password` | Password for the seeded admin |
| `Seed__AdminUser__DisplayName` | Display name for the seeded admin |
| `Seed__AdminUser__ResetPasswordOnExistingUser` | Obsolete and ignored; seed reruns preserve existing passwords, status and privileges |

For examples and deployment context, see [Environment variables](../configuration/environment-variables.md) and [Production deployment](../installation/production.md).
