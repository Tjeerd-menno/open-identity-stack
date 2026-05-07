# Data Model: OpenIddict-Based IAM

**Feature**: 001-openiddict-iam  
**Created**: 2026-01-18

## Overview

This document defines the domain entities, their relationships, validation rules, and state transitions for the IAM solution.

---

## Entity Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              USERS DOMAIN                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────┐     1:N      ┌───────────────────┐                        │
│  │     User     │──────────────│  UpstreamIdentity │                        │
│  │  (Aggregate) │              │   (Value Object)  │                        │
│  └──────────────┘              └───────────────────┘                        │
│         │                                                                    │
│         │ N:M                                                                │
│         ▼                                                                    │
│  ┌──────────────┐                                                           │
│  │     Role     │◄─────────────────────────────────────────────┐            │
│  │   (Entity)   │                                              │            │
│  └──────────────┘                                              │            │
│         ▲                                                      │            │
│         │ mapped from                                          │            │
│         │                                                      │            │
│  ┌──────────────┐     1:N      ┌───────────────────┐          │            │
│  │    Group     │──────────────│   GroupMapping    │──────────┘            │
│  │  (Aggregate) │              │  (role/claim map) │                        │
│  └──────────────┘              └───────────────────┘                        │
│         │                                                                    │
│         │ N:M (membership)                                                   │
│         ▼                                                                    │
│  ┌──────────────┐                                                           │
│  │     User     │                                                           │
│  └──────────────┘                                                           │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                           SERVICE ACCOUNTS DOMAIN                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────┐     1:N      ┌───────────────────┐                    │
│  │  ServiceAccount  │──────────────│  ClientCredential │                    │
│  │    (Aggregate)   │              │   (Value Object)  │                    │
│  └──────────────────┘              └───────────────────┘                    │
│         │                                                                    │
│         │ 1:N                                                                │
│         ▼                                                                    │
│  ┌──────────────────┐                                                       │
│  │ ClientCertificate│                                                       │
│  │  (Value Object)  │                                                       │
│  └──────────────────┘                                                       │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                            FEDERATION DOMAIN                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────┐     1:N      ┌───────────────────┐                    │
│  │ UpstreamProvider │──────────────│   ClaimMapping    │                    │
│  │    (Aggregate)   │              │  (Value Object)   │                    │
│  └──────────────────┘              └───────────────────┘                    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                             SESSIONS DOMAIN                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────┐     1:N      ┌───────────────────┐                    │
│  │   UserSession    │──────────────│   ClientSession   │                    │
│  │    (Aggregate)   │              │  (tracks RP login)│                    │
│  └──────────────────┘              └───────────────────┘                    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Entities

### User (Aggregate Root)

Represents a person who can authenticate to the system.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | UserId (GUID) | PK, required | Unique identifier |
| Email | string | Unique, max 256, email format | Primary contact email |
| NormalizedEmail | string | Unique, max 256 | Uppercase for lookups |
| DisplayName | string | Max 256 | Human-readable name |
| PasswordHash | string? | Nullable | Null for federated-only users |
| Status | UserStatus | Required | Active, Disabled, PendingVerification |
| CreatedAt | DateTimeOffset | Required | Record creation time |
| ModifiedAt | DateTimeOffset | Required | Last modification time |
| LastLoginAt | DateTimeOffset? | Nullable | Last successful login |

**Relationships:**
- Has many `UpstreamIdentity` (0..N)
- Has many `Role` via `RoleAssignment` (N:M)
- Has many `Group` via `GroupMembership` (N:M)
- Has many `UserSession` (1:N)

**State Transitions:**
```
PendingVerification ──(verify)──► Active
Active ──(disable)──► Disabled
Disabled ──(enable)──► Active
Active ──(delete)──► [Soft Deleted]
```

**Validation Rules:**
- Email must be valid email format
- DisplayName cannot be empty
- Password (when set) must meet complexity requirements
- Cannot delete user with active sessions (must revoke first)

---

### UpstreamIdentity (Value Object)

Links a User to an external identity provider.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Issuer | string | Required, max 512 | IdP issuer URL |
| Subject | string | Required, max 256 | Subject claim from IdP |
| LinkedAt | DateTimeOffset | Required | When link was established |
| LastUsedAt | DateTimeOffset? | Nullable | Last login via this identity |

**Composite Key**: (UserId, Issuer, Subject)

**Validation Rules:**
- (Issuer, Subject) must be unique across all users
- Cannot link same upstream identity to multiple users

---

### Role (Entity)

Represents a named permission grouping.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | RoleId (GUID) | PK, required | Unique identifier |
| Name | string | Unique, max 128 | Role name (e.g., "UserAdmin") |
| Description | string? | Max 512 | Human-readable description |
| IsSystemRole | bool | Default false | Cannot be deleted if true |
| CreatedAt | DateTimeOffset | Required | Record creation time |

**Relationships:**
- Has many `User` via `RoleAssignment` (N:M)
- Has many `GroupMapping` (as target)

**Validation Rules:**
- Name must be alphanumeric with dots/underscores
- System roles cannot be deleted
- Role name is case-insensitive unique

---

### RoleAssignment (Join Entity)

Links User to Role with metadata.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| UserId | UserId | FK, required | User reference |
| RoleId | RoleId | FK, required | Role reference |
| AssignedAt | DateTimeOffset | Required | When assignment was made |
| AssignedBy | UserId | Required | Admin who made assignment |

**Composite Key**: (UserId, RoleId)

---

### Group (Aggregate Root)

Represents a collection of users for organizational and authorization purposes.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | GroupId (GUID) | PK, required | Unique identifier |
| Name | string | Unique, max 128 | Group name |
| Description | string? | Max 512 | Human-readable description |
| ParentGroupId | GroupId? | FK, nullable | Parent group for hierarchy |
| CreatedAt | DateTimeOffset | Required | Record creation time |
| ModifiedAt | DateTimeOffset | Required | Last modification time |

**Relationships:**
- Has many `User` via `GroupMembership` (N:M)
- Has many `GroupMapping` (1:N)
- Has one optional `ParentGroup` (self-referential)
- Has many `ChildGroups` (self-referential)

**Validation Rules:**
- Name must be unique within scope
- Circular parent references not allowed
- Maximum hierarchy depth: 5 levels

---

### GroupMembership (Join Entity)

Links User to Group with metadata.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| UserId | UserId | FK, required | User reference |
| GroupId | GroupId | FK, required | Group reference |
| JoinedAt | DateTimeOffset | Required | When membership was added |
| AddedBy | UserId | Required | Admin who added member |

**Composite Key**: (UserId, GroupId)

---

### GroupMapping (Value Object)

Defines mapping from Group to Roles or Claims.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | Guid | PK, required | Unique identifier |
| GroupId | GroupId | FK, required | Source group |
| MappingType | MappingType | Required | Role or Claim |
| TargetRoleId | RoleId? | FK, conditional | Target role (if type=Role) |
| ClaimType | string? | Max 256, conditional | Claim type (if type=Claim) |
| ClaimValue | string? | Max 1024, conditional | Claim value (if type=Claim) |
| TokenTarget | TokenTarget | Required | AccessToken, IdToken, or Both |
| Priority | int | Default 0 | For conflict resolution |

**Validation Rules:**
- If MappingType=Role, TargetRoleId required
- If MappingType=Claim, ClaimType and ClaimValue required
- Higher priority wins in conflicts

---

### ServiceAccount (Aggregate Root)

Represents a non-human client entity (machine user).

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | ServiceAccountId (GUID) | PK, required | Unique identifier |
| ClientId | string | Unique, max 128 | OAuth2 client_id |
| DisplayName | string | Max 256 | Human-readable name |
| Status | ServiceAccountStatus | Required | Active, Disabled |
| AllowedScopes | string[] | Required | Permitted OAuth2 scopes |
| AllowedGrantTypes | string[] | Required | Permitted grant types |
| CreatedAt | DateTimeOffset | Required | Record creation time |
| ModifiedAt | DateTimeOffset | Required | Last modification time |

**Relationships:**
- Has many `ClientCredential` (1:N)
- Has many `ClientCertificate` (1:N)

**Validation Rules:**
- ClientId must be unique
- At least one credential (secret or certificate) required for active accounts
- AllowedGrantTypes must be valid OAuth2 grant types

---

### ClientCredential (Value Object)

Stores client secret for ServiceAccount.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | Guid | PK, required | Unique identifier |
| ServiceAccountId | ServiceAccountId | FK, required | Parent service account |
| SecretHash | string | Required | Hashed client secret |
| Description | string? | Max 256 | Purpose of this credential |
| ExpiresAt | DateTimeOffset? | Nullable | Optional expiration |
| CreatedAt | DateTimeOffset | Required | Record creation time |
| LastUsedAt | DateTimeOffset? | Nullable | Last successful auth |
| IsRevoked | bool | Default false | Whether revoked |

**Validation Rules:**
- Secret stored as hash only
- Expired/revoked credentials rejected at auth

---

### ClientCertificate (Value Object)

Stores certificate metadata for ServiceAccount.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | Guid | PK, required | Unique identifier |
| ServiceAccountId | ServiceAccountId | FK, required | Parent service account |
| Thumbprint | string | Required, max 64 | SHA-256 thumbprint |
| Subject | string | Required, max 512 | Certificate subject |
| ExpiresAt | DateTimeOffset | Required | Certificate expiration |
| CreatedAt | DateTimeOffset | Required | Record creation time |
| IsRevoked | bool | Default false | Whether revoked |

**Validation Rules:**
- Thumbprint must be unique per service account
- Expired/revoked certificates rejected at auth

---

### UpstreamProvider (Aggregate Root)

Configuration for an external OIDC identity provider.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | UpstreamProviderId (GUID) | PK, required | Unique identifier |
| Name | string | Unique, max 128 | Display name |
| Slug | string | Unique, max 64 | URL-safe identifier |
| Authority | string | Required, max 512, URL | IdP issuer URL |
| ClientId | string | Required, max 256 | OAuth2 client_id |
| ClientSecretEncrypted | string? | Encrypted | Encrypted client secret |
| Scopes | string[] | Required | Requested scopes |
| JitProvisioningEnabled | bool | Default true | Enable JIT user creation |
| Status | ProviderStatus | Required | Active, Disabled |
| CreatedAt | DateTimeOffset | Required | Record creation time |
| ModifiedAt | DateTimeOffset | Required | Last modification time |

**Relationships:**
- Has many `ClaimMapping` (1:N)

**Validation Rules:**
- Authority must be valid HTTPS URL
- Discovery metadata must be reachable on save
- Slug must be URL-safe (alphanumeric, hyphens)

---

### ClaimMapping (Value Object)

Transforms upstream claims to local token claims.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | Guid | PK, required | Unique identifier |
| ProviderId | UpstreamProviderId | FK, required | Parent provider |
| SourceClaimType | string | Required, max 256 | Upstream claim type |
| TargetClaimType | string | Required, max 256 | Local claim type |
| TransformType | TransformType | Required | Direct, Prefix, Regex |
| TransformPattern | string? | Max 512 | Pattern for transform |

**Validation Rules:**
- SourceClaimType cannot be empty
- If TransformType=Regex, TransformPattern required and valid

---

### UserSession (Aggregate Root)

Represents an authenticated user session at the IAM.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | SessionId (GUID) | PK, required | Unique identifier |
| UserId | UserId | FK, required | Session owner |
| Status | SessionStatus | Required | Active, Revoked, Expired |
| AuthenticationMethod | string | Required | "password", "federated:provider-slug" |
| IpAddress | string? | Max 45 | Client IP at login |
| UserAgent | string? | Max 512 | Client user agent |
| CreatedAt | DateTimeOffset | Required | Session start time |
| ExpiresAt | DateTimeOffset | Required | Session expiration |
| RevokedAt | DateTimeOffset? | Nullable | When revoked |

**Relationships:**
- Belongs to `User` (N:1)
- Has many `ClientSession` (1:N) - tracks which RPs user logged into

**State Transitions:**
```
Active ──(time passes)──► Expired
Active ──(revoke)──► Revoked
```

---

### ClientSession (Value Object)

Tracks relying party logins within a user session (for SLO).

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | Guid | PK, required | Unique identifier |
| SessionId | SessionId | FK, required | Parent session |
| ClientId | string | Required, max 128 | RP client_id |
| LoginAt | DateTimeOffset | Required | When user logged in to RP |
| LogoutAt | DateTimeOffset? | Nullable | When logout notification sent |
| LogoutStatus | LogoutStatus? | Nullable | Pending, Success, Failed |

---

## Enums

### UserStatus
- `PendingVerification` = 0
- `Active` = 1
- `Disabled` = 2

### ServiceAccountStatus
- `Active` = 1
- `Disabled` = 2

### ProviderStatus
- `Active` = 1
- `Disabled` = 2

### SessionStatus
- `Active` = 1
- `Revoked` = 2
- `Expired` = 3

### MappingType
- `Role` = 1
- `Claim` = 2

### TokenTarget
- `AccessToken` = 1
- `IdToken` = 2
- `Both` = 3

### TransformType
- `Direct` = 1 (copy as-is)
- `Prefix` = 2 (add prefix)
- `Regex` = 3 (regex replace)

### LogoutStatus
- `Pending` = 1
- `Success` = 2
- `Failed` = 3

---

## Indexes

| Entity | Index | Columns | Unique |
|--------|-------|---------|--------|
| User | IX_User_NormalizedEmail | NormalizedEmail | Yes |
| User | IX_User_Status | Status | No |
| UpstreamIdentity | IX_UpstreamIdentity_Issuer_Subject | Issuer, Subject | Yes |
| Role | IX_Role_Name | Name | Yes |
| Group | IX_Group_Name | Name | Yes |
| Group | IX_Group_ParentGroupId | ParentGroupId | No |
| ServiceAccount | IX_ServiceAccount_ClientId | ClientId | Yes |
| ClientCredential | IX_ClientCredential_ServiceAccountId | ServiceAccountId | No |
| ClientCertificate | IX_ClientCertificate_Thumbprint | ServiceAccountId, Thumbprint | Yes |
| UpstreamProvider | IX_UpstreamProvider_Slug | Slug | Yes |
| UserSession | IX_UserSession_UserId | UserId | No |
| UserSession | IX_UserSession_Status_ExpiresAt | Status, ExpiresAt | No |

---

## Audit Fields

All aggregate roots include:
- `CreatedAt`: DateTimeOffset (set on create)
- `ModifiedAt`: DateTimeOffset (updated on every change)

Security-sensitive operations also record:
- Actor (who performed the action)
- IP address
- User agent
