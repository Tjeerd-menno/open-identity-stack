# Data Model: Unify Applications Domain

## Overview

The feature introduces `Application` as the single aggregate root for administrator-managed OAuth/OIDC software registrations. Existing clients and service accounts are migrated into this model. Credentials and certificates become child entities of an application.

## Entity: Application

**Purpose**: Represents one registered software system that can participate in OAuth/OIDC flows.

### Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `ApplicationId` | Yes | Strongly typed identifier. Preserve source `Client.Id`/`ServiceAccount.Id` when safe. |
| `ClientId` | string | Yes | Stable external OAuth/OIDC protocol identifier; unique and immutable after creation. |
| `DisplayName` | string | Yes | Administrator-facing name. |
| `Description` | string? | No | Optional administrator-facing description. |
| `Type` | `ApplicationProfile` | Yes | Product profile: `MachineToMachine`, `Web`, `SinglePage`, `Native`, `Device`, or `Custom`. |
| `ClientType` | `OAuthClientType` | Yes | Protocol confidentiality classification: `Public` or `Confidential`. |
| `Status` | `ApplicationStatus` | Yes | `Active` or `Disabled`. Disabled blocks new token issuance. |
| `AllowedGrantTypes` | collection | Yes | OAuth grant behavior allowed for the application. |
| `AllowedScopes` | collection | Yes | Scopes this application may request. |
| `RedirectUris` | collection | Yes | Empty when not applicable. Required for authorization-code apps. |
| `PostLogoutRedirectUris` | collection | Yes | Empty when not applicable. Not allowed for machine-to-machine apps. |
| `RequirePkce` | bool | Yes | Required for public authorization-code applications. |
| `RequireConsent` | bool | Yes | Configurable for interactive apps; false for machine-to-machine apps. |
| `RequiresMigrationReview` | bool | Yes | True when migrated data cannot be confidently normalized. |
| `MigrationSource` | string? | No | `Client`, `ServiceAccount`, or null for new applications. |
| `CreatedAt` | timestamp | Yes | Preserve source value during migration. |
| `ModifiedAt` | timestamp? | No | Preserve source value when available. |

### Relationships

- One `Application` has zero or more `ApplicationCredential` child entities.
- One `Application` may have related audit events.
- The OpenIddict application is a projection of `Application`, not a separate domain source of truth.

### Validation Rules

- `ClientId` is required, trimmed, unique, immutable, externally stable, and limited to 255 characters.
- `DisplayName` is required, trimmed, and limited to 255 characters.
- `Description` is optional and limited to 1000 characters.
- `AllowedGrantTypes` must not be empty for an active application.
- `AllowedScopes` must not contain null, empty, or whitespace values.
- `client_credentials` requires `ClientType = Confidential`.
- Machine-to-machine applications must be confidential, may only use `client_credentials` in this release, and must not define redirect or post-logout redirect URIs.
- `authorization_code` requires at least one redirect URI.
- Public authorization-code applications require PKCE.
- Public applications must not have secrets, certificates, private-key JWT credentials, or other confidential-client credentials.
- `implicit` and `password` are rejected for new default applications unless explicitly enabled through legacy/custom behavior.
- `device_code` is allowed only for `Device` or explicitly custom applications.

### State Transitions

| From | Action | To | Notes |
|------|--------|----|-------|
| New | Create | Active | Default for valid new application. |
| Active | Disable | Disabled | Blocks new token issuance but preserves configuration and credentials. |
| Disabled | Enable | Active | Re-enables token issuance if configuration remains valid. |
| Active/Disabled | Delete | Deleted | Recommended hard delete for parity with current APIs; audit retained separately. |

## Entity: ApplicationCredential

**Purpose**: Represents secret, certificate, or future assertion credential material used by a confidential application.

### Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | Guid | Yes | Preserve source credential/certificate ID where safe. |
| `ApplicationId` | `ApplicationId` | Yes | Parent application. |
| `Type` | `ApplicationCredentialType` | Yes | `ClientSecret`, `X509Certificate`, or reserved future type. |
| `SecretHash` | string? | Conditional | Present only for client secrets; plain secret is returned once and never stored. |
| `Thumbprint` | string? | Conditional | Required for certificate credentials. |
| `Subject` | string? | No | Certificate subject or credential subject metadata. |
| `Description` | string? | No | Administrator-facing description. |
| `ExpiresAt` | timestamp? | No | Null means no explicit expiry. |
| `CreatedAt` | timestamp | Yes | Preserve source value during migration. |
| `LastUsedAt` | timestamp? | No | Updated on successful credential use. |
| `RevokedAt` | timestamp? | No | Non-null means credential is revoked. |

### Relationships

- Belongs to exactly one `Application`.
- Used by application client-authentication validation.

### Validation Rules

- Credentials are allowed only on confidential applications.
- A confidential application may have multiple credentials for rotation.
- Plain client secrets are returned only once at creation/rotation.
- Client secrets are stored only as hashes.
- Revoked credentials are never accepted.
- Expired credentials are never accepted.
- Certificate thumbprints are unique per active application credential set.
- Certificate credentials store thumbprint, subject, expiration, created timestamp, and revocation timestamp.

### State Transitions

| From | Action | To | Notes |
|------|--------|----|-------|
| New | Add credential | Active | Credential can authenticate until revoked or expired. |
| Active | Expiry time passes | Expired | Rejected by validation. |
| Active | Revoke | Revoked | Rejected by validation immediately. |
| Active | Successful use | Active | Updates `LastUsedAt`. |

## Entity: Application Permission Mapping

**Purpose**: Maps legacy client/service-account permissions to unified application permissions.

### Target Permissions

| Permission | Purpose |
|------------|---------|
| `applications:read` | List and view applications. |
| `applications:write` | Create, update, enable, disable, and configure applications. |
| `applications:delete` | Delete applications. |
| `applications:manage-credentials` | Add/rotate/revoke client-secret credentials. |
| `applications:manage-certificates` | Add/revoke certificate credentials. |
| `applications:*` | Wildcard for all application operations. |

### Migration Mapping

| Legacy permission | New permission |
|-------------------|----------------|
| `clients:read` | `applications:read` |
| `clients:write` | `applications:write` |
| `clients:delete` | `applications:delete` |
| `clients:manage-secrets` | `applications:manage-credentials` |
| `clients:*` | `applications:*` |
| `service-accounts:read` | `applications:read` |
| `service-accounts:write` | `applications:write` |
| `service-accounts:delete` | `applications:delete` |
| `service-accounts:rotate-secret` | `applications:manage-credentials` |
| `service-accounts:manage-certificates` | `applications:manage-certificates` |
| `service-accounts:*` | `applications:*` |

## Entity: OpenIddict Application Projection

**Purpose**: Infrastructure projection used by OpenIddict for protocol behavior.

### Mapping

| Application field | Projection behavior |
|-------------------|---------------------|
| `ClientId` | Projection client ID. |
| `DisplayName` | Projection display name. |
| `ClientType = Confidential` | Confidential client type. |
| `ClientType = Public` | Public client type. |
| `RedirectUris` | Redirect URI set. |
| `PostLogoutRedirectUris` | Post-logout redirect URI set. |
| `RequireConsent` | Explicit or implicit consent behavior. |
| `RequirePkce` | PKCE requirement. |
| `AllowedGrantTypes` | Endpoint, grant, and response-type permissions. |
| `AllowedScopes` | Scope permissions. |
| `Status = Disabled` | Token issuance rejected by validation path. |

### Consistency Rules

- Create/update/delete application use cases update both domain persistence and projection.
- Credential add/revoke operations must not allow token validation to diverge from the domain store.
- If projection update fails after domain mutation, the use case must surface failure consistently and preserve retry/repair visibility.

## Migration Model

### Preflight Checks

- Detect duplicate client identifiers across legacy clients and service accounts before mutating data.
- Detect service accounts with grants other than `client_credentials`.
- Detect invalid or ambiguous grant/profile combinations.
- Produce an actionable report and fail strict production migration before writing new application data.

### Client Migration Mapping

| Existing source | Application target |
|-----------------|--------------------|
| `Clients.Id` | Not preserved; generate a new `Applications.Id`. |
| `Clients.ClientIdValue` | `Applications.ClientId` |
| `Clients.DisplayName` | `Applications.DisplayName` |
| `Clients.Description` | `Applications.Description` |
| `Clients.ClientType` | `Applications.ClientType` |
| `Clients.RedirectUris` | `Applications.RedirectUris` |
| `Clients.PostLogoutRedirectUris` | `Applications.PostLogoutRedirectUris` |
| `Clients.AllowedScopes` | `Applications.AllowedScopes` |
| `Clients.AllowedGrantTypes` | `Applications.AllowedGrantTypes` |
| `Clients.RequirePkce` | `Applications.RequirePkce` |
| `Clients.RequireConsent` | `Applications.RequireConsent` |
| `Clients.CreatedAt` | `Applications.CreatedAt` |
| `Clients.ModifiedAt` | `Applications.ModifiedAt` |

### Client Type Inference

| Condition | Application profile | Requires review |
|-----------|------------------|-----------------|
| Confidential and only `client_credentials` | MachineToMachine | No |
| Confidential and has `authorization_code` | Web | No |
| Public and has `authorization_code` | SinglePage | Yes |
| Has `device_code` | Device | No |
| Has `implicit` or `password` | Custom | Yes |
| Any other combination | Custom | Yes |

### Service Account Migration Mapping

| Existing source | Application target |
|-----------------|--------------------|
| `ServiceAccounts.Id` | Not preserved; generate a new `Applications.Id`. |
| `ServiceAccounts.ClientId` | `Applications.ClientId` |
| `ServiceAccounts.DisplayName` | `Applications.DisplayName` |
| `ServiceAccounts.Status` | `Applications.Status` |
| `ServiceAccounts.AllowedScopes` | `Applications.AllowedScopes` |
| `ServiceAccounts.AllowedGrantTypes` | `Applications.AllowedGrantTypes` only for supported `client_credentials` data; no compatibility normalization. |
| `ServiceAccounts.CreatedAt` | `Applications.CreatedAt` |
| `ServiceAccounts.ModifiedAt` | `Applications.ModifiedAt` |
| N/A | `Type = MachineToMachine` |
| N/A | `ClientType = Confidential` |
| N/A | `RedirectUris = []` |
| N/A | `PostLogoutRedirectUris = []` |
| N/A | `RequirePkce = false` |
| N/A | `RequireConsent = false` |

### Credential Migration Mapping

| Existing source | ApplicationCredential target |
|-----------------|------------------------------|
| `ClientCredentials.Id` | `Id` |
| `ClientCredentials.ServiceAccountId` | mapped `ApplicationId` |
| `ClientCredentials.SecretHash` | `SecretHash` |
| `ClientCredentials.Description` | `Description` |
| `ClientCredentials.ExpiresAt` | `ExpiresAt` |
| `ClientCredentials.CreatedAt` | `CreatedAt` |
| `ClientCredentials.LastUsedAt` | `LastUsedAt` |
| `ClientCredentials.IsRevoked` | `RevokedAt = migration timestamp` when true |
| `ClientCertificates.Id` | `Id` |
| `ClientCertificates.ServiceAccountId` | mapped `ApplicationId` |
| `ClientCertificates.Thumbprint` | `Thumbprint` |
| `ClientCertificates.Subject` | `Subject` and `Description` |
| `ClientCertificates.ExpiresAt` | `ExpiresAt` |
| `ClientCertificates.CreatedAt` | `CreatedAt` |
| `ClientCertificates.IsRevoked` | `RevokedAt = migration timestamp` when true |

## Audit Event Model

| Event | Trigger |
|-------|---------|
| `ApplicationCreated` | Application creation. |
| `ApplicationUpdated` | Metadata or OAuth configuration change. |
| `ApplicationDisabled` | Disable action. |
| `ApplicationEnabled` | Enable action. |
| `ApplicationDeleted` | Delete action. |
| `ApplicationCredentialAdded` | Secret/certificate added. |
| `ApplicationCredentialRevoked` | Secret/certificate revoked. |
| `ApplicationCredentialUsed` | Successful credential authentication where usage tracking is enabled. |

Audit payloads must include actor, target application ID/client ID, timestamp, action, and non-sensitive metadata. Plain secrets and secret hashes must never appear in audit payloads.
