# Environment variables

The table below captures the most important deployment variables surfaced by the current product and deployment manifests.

| Variable | Purpose | Typical use |
| --- | --- | --- |
| `ConnectionStrings__openidentitystack` | PostgreSQL connection string | API and DbMigrator in all environments |
| `OPENIDENTITYSTACK_DISABLE_DATA_VOLUME` | Disable persistent local PostgreSQL volume | Disposable local runs |
| `OPENIDENTITYSTACK_ENABLE_MANAGEMENTWEB` | Disable management web in the local AppHost | Backend-only local runs |
| `ForwardedHeaders__Enabled` | Enable trusted proxy header processing | Reverse proxy or ingress deployments |
| `ForwardedHeaders__KnownProxies__0` | Trust a specific proxy IP | Hardened ingress setup |
| `ForwardedHeaders__KnownNetworks__0` | Trust a specific proxy CIDR | Hardened ingress setup |
| `AllowedCorsOrigins` | Comma-separated browser origins | Production management web access |
| `VITE_OIDC_AUTHORITY` | Management web authority URL | Local AppHost or containerized management web |
| `VITE_API_BASE_URL` | Management web API base URL | Local AppHost or containerized management web |
| `Seed__DevelopmentData` | Enables development seed behavior | Local composition |
| `Seed__AdminUser__Enabled` | Enables first-run admin seed | Production bootstrap |
| `Seed__AdminUser__Email` | Seed admin email | Production bootstrap |
| `Seed__AdminUser__Password` | Seed admin password | Production bootstrap |
| `Seed__AdminUser__DisplayName` | Seed admin display name | Production bootstrap |
| `Seed__AdminUser__ResetPasswordOnExistingUser` | Obsolete; existing accounts are preserved | Remove this setting; use authorized password recovery |
| `OpenIddict__Certificates__Signing__CertificatePath` | PEM certificate path for signing | Production token signing |
| `OpenIddict__Certificates__Signing__PrivateKeyPath` | PEM private key path for signing | Production token signing |
| `OpenIddict__Certificates__Encryption__CertificatePath` | PEM certificate path for encryption | Production token encryption |
| `OpenIddict__Certificates__Encryption__PrivateKeyPath` | PEM private key path for encryption | Production token encryption |

For Kubernetes-specific examples, use the manifests and README under `deploy/open-identity-stack`.
