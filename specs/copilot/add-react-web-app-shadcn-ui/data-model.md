# Data Model: React Admin Web App

**Date**: 2026-01-18  
**Purpose**: Define TypeScript data models for the React admin application based on existing API contracts.

---

## Overview

This document defines the TypeScript types and interfaces for the admin web application. These models are derived from the existing admin API endpoints in the OpenIdentityStack.Api project.

---

## 1. Common Types

### Pagination

```typescript
/**
 * Generic paginated response
 */
export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage?: boolean;
  hasNextPage?: boolean;
}

/**
 * Pagination parameters for requests
 */
export interface PaginationParams {
  page?: number;        // Default: 1
  pageSize?: number;    // Default: 20
  search?: string;      // Optional search term
}
```

### API Response

```typescript
/**
 * Standard API error response
 */
export interface ApiError {
  type: string;
  title: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>;
}

/**
 * Result wrapper for API operations
 */
export type ApiResult<T> = 
  | { success: true; data: T }
  | { success: false; error: ApiError };
```

---

## 2. User Management

### User Entity

```typescript
/**
 * Full user details
 */
export interface User {
  id: string;               // Guid
  email: string;
  displayName: string;
  status: UserStatus;
  mfaEnabled: boolean;
  lastLoginAt: string | null;  // ISO 8601 timestamp
  createdAt: string;           // ISO 8601 timestamp
  modifiedAt: string | null;   // ISO 8601 timestamp
}

/**
 * User status enumeration
 */
export enum UserStatus {
  PendingVerification = 'PendingVerification',
  Active = 'Active',
  Disabled = 'Disabled',
  Locked = 'Locked'
}

/**
 * User list item (lighter than full User)
 */
export interface UserListItem {
  id: string;
  email: string;
  displayName: string;
  status: UserStatus;
  createdAt: string;
}
```

### User Requests

```typescript
/**
 * Create user request
 */
export interface CreateUserRequest {
  email: string;
  displayName: string;
  password: string;
}

/**
 * Update user request
 */
export interface UpdateUserRequest {
  displayName?: string;
}

/**
 * Disable user request
 */
export interface DisableUserRequest {
  reason: string;
}

/**
 * Reset password request
 */
export interface ResetPasswordRequest {
  newPassword: string;
}

/**
 * Link upstream identity request
 */
export interface LinkUpstreamIdentityRequest {
  providerId: string;    // Guid
  subjectId: string;
  email?: string;
}
```

### User Responses

```typescript
/**
 * Paginated user list response
 */
export type UserListResponse = PaginatedResponse<UserListItem>;

/**
 * User status change response
 */
export interface UserStatusChangeResponse {
  userId: string;
  timestamp: string;
}

/**
 * Password reset response
 */
export interface PasswordResetResponse {
  userId: string;
  resetAt: string;
}
```

### Upstream Identities

```typescript
/**
 * Upstream identity (federated login)
 */
export interface UpstreamIdentity {
  providerId: string;       // Guid
  providerName: string;
  subjectId: string;
  email: string | null;
  linkedAt: string;         // ISO 8601
  lastLoginAt: string | null;
}

/**
 * Link upstream identity response
 */
export interface LinkUpstreamIdentityResponse {
  userId: string;
  providerId: string;
  subjectId: string;
  linkedAt: string;
}
```

---

## 3. Role Management

### Role Entity

```typescript
/**
 * Role details
 */
export interface Role {
  id: string;                  // Guid
  name: string;                // Normalized name
  displayName: string;
  description: string | null;
  isSystemRole: boolean;       // System roles cannot be deleted
  isActive: boolean;
  permissions: string[];       // e.g., ["users:read", "users:create"]
}

/**
 * Simplified role for assignments
 */
export interface RoleListItem {
  id: string;
  name: string;
  displayName: string;
  isSystemRole: boolean;
  isActive: boolean;
}
```

### Role Requests

```typescript
/**
 * Create role request
 */
export interface CreateRoleRequest {
  name: string;
  displayName: string;
  description?: string;
  permissions: string[];
}

/**
 * Update role request
 */
export interface UpdateRoleRequest {
  displayName?: string;
  description?: string;
  permissions?: string[];
}
```

### Role Responses

```typescript
/**
 * Paginated role list response
 */
export type RoleListResponse = PaginatedResponse<RoleListItem>;

/**
 * User roles response (for a specific user)
 */
export interface UserRolesResponse {
  userId: string;
  roles: Role[];
}
```

---

## 4. Group Management

### Group Entity

```typescript
/**
 * Group details
 */
export interface Group {
  id: string;                  // Guid
  name: string;
  description: string | null;
  memberCount: number;
  mappingCount: number;        // Role/claim mappings
  createdAt: string;
  modifiedAt: string | null;
}

/**
 * Group member
 */
export interface GroupMember {
  userId: string;
  email: string;
  displayName: string;
  addedAt: string;
}

/**
 * Group mapping (role or claim assignment)
 */
export interface GroupMapping {
  id: string;
  type: MappingType;
  value: string;               // Role ID or claim value
  createdAt: string;
}

export enum MappingType {
  Role = 'Role',
  Claim = 'Claim'
}
```

### Group Requests

```typescript
/**
 * Create group request
 */
export interface CreateGroupRequest {
  name: string;
  description?: string;
}

/**
 * Update group request
 */
export interface UpdateGroupRequest {
  name?: string;
  description?: string;
}

/**
 * Add user to group request
 */
export interface AddUserToGroupRequest {
  userId: string;
}

/**
 * Add group mapping request
 */
export interface AddGroupMappingRequest {
  type: MappingType;
  value: string;
}
```

### Group Responses

```typescript
/**
 * Paginated group list response
 */
export type GroupListResponse = PaginatedResponse<Group>;

/**
 * User groups response (for a specific user)
 */
export interface UserGroupsResponse {
  userId: string;
  groups: Group[];
}
```

---

## 5. Service Account Management

### Service Account Entity

```typescript
/**
 * Service account details
 */
export interface ServiceAccount {
  id: string;                     // Guid
  clientId: string;
  displayName: string;
  status: ServiceAccountStatus;
  allowedScopes: string[];        // e.g., ["admin-api", "openid"]
  allowedGrantTypes: string[];    // e.g., ["client_credentials"]
  credentialCount: number;        // Number of secrets
  certificateCount: number;
  createdAt: string;
  modifiedAt: string | null;
}

export enum ServiceAccountStatus {
  Active = 'Active',
  Disabled = 'Disabled'
}
```

### Service Account Requests

```typescript
/**
 * Create service account request
 */
export interface CreateServiceAccountRequest {
  clientId: string;
  displayName: string;
  allowedScopes: string[];
  allowedGrantTypes: string[];
}

/**
 * Update service account request
 */
export interface UpdateServiceAccountRequest {
  displayName?: string;
  allowedScopes?: string[];
}

/**
 * Rotate secret request
 */
export interface RotateSecretRequest {
  revokeExisting?: boolean;
  description?: string;
  expiresAt?: string;  // ISO 8601
}

/**
 * Add certificate request
 */
export interface AddCertificateRequest {
  thumbprint: string;
  subject: string;
  expiresAt: string;   // ISO 8601
}
```

### Service Account Responses

```typescript
/**
 * Service account created response (includes initial secret)
 */
export interface ServiceAccountCreatedResponse {
  id: string;
  clientId: string;
  displayName: string;
  initialSecret: string;  // Only returned once at creation!
  createdAt: string;
}

/**
 * Paginated service account list response
 */
export type ServiceAccountListResponse = PaginatedResponse<ServiceAccount>;

/**
 * Rotate secret response
 */
export interface RotateSecretResponse {
  credentialId: string;
  newSecret: string;      // Only returned once!
}

/**
 * Add certificate response
 */
export interface AddCertificateResponse {
  certificateId: string;
}
```

---

## 6. Session Management

### Session Entity

```typescript
/**
 * User session details
 */
export interface Session {
  id: string;                 // Guid
  userId: string;
  ipAddress: string;
  userAgent: string;
  status: SessionStatus;
  clientCount: number;        // Number of OAuth clients in this session
  lastActivityAt: string;     // ISO 8601
  expiresAt: string;
  createdAt: string;
}

export enum SessionStatus {
  Active = 'Active',
  Expired = 'Expired',
  Revoked = 'Revoked'
}
```

### Session Responses

```typescript
/**
 * Paginated session list response
 */
export type SessionListResponse = PaginatedResponse<Session>;

/**
 * Revoke session response
 */
export interface RevokeSessionResponse {
  sessionId: string;
  revokedAt: string;
}

/**
 * Revoke all sessions response
 */
export interface RevokeAllSessionsResponse {
  revokedCount: number;
  revokedAt: string;
}
```

---

## 7. Provider Management (Identity Providers)

### Provider Entity

```typescript
/**
 * Identity provider configuration
 */
export interface Provider {
  id: string;                    // Guid
  name: string;                  // Normalized name (e.g., "google")
  displayName: string;
  type: ProviderType;
  enabled: boolean;
  authority?: string;            // For OIDC providers
  clientId?: string;
  metadataUrl?: string;
  configuration: Record<string, unknown>;  // Provider-specific config
  createdAt: string;
  modifiedAt: string | null;
}

export enum ProviderType {
  OIDC = 'OIDC',
  OAuth2 = 'OAuth2',
  SAML2 = 'SAML2'
}
```

### Provider Requests

```typescript
/**
 * Create provider request
 */
export interface CreateProviderRequest {
  name: string;
  displayName: string;
  type: ProviderType;
  enabled?: boolean;
  authority?: string;
  clientId?: string;
  clientSecret?: string;  // Stored securely, not returned
  metadataUrl?: string;
  configuration?: Record<string, unknown>;
}

/**
 * Update provider request
 */
export interface UpdateProviderRequest {
  displayName?: string;
  enabled?: boolean;
  authority?: string;
  clientId?: string;
  clientSecret?: string;
  metadataUrl?: string;
  configuration?: Record<string, unknown>;
}
```

### Provider Responses

```typescript
/**
 * Paginated provider list response
 */
export type ProviderListResponse = PaginatedResponse<Provider>;
```

---

## 8. Authentication (Frontend-Only)

### Auth Context

```typescript
/**
 * Authenticated user info (from OIDC claims)
 */
export interface AuthenticatedUser {
  sub: string;              // Subject (user ID)
  email: string;
  name: string;             // Display name
  permissions: string[];    // Granted permissions
}

/**
 * Authentication state
 */
export interface AuthState {
  isAuthenticated: boolean;
  isLoading: boolean;
  user: AuthenticatedUser | null;
  accessToken: string | null;
  error: string | null;
}
```

---

## Validation Schemas (Zod)

### User Validation

```typescript
import { z } from 'zod';

export const createUserSchema = z.object({
  email: z.string().email('Invalid email address'),
  displayName: z.string().min(1, 'Display name is required').max(100),
  password: z.string()
    .min(8, 'Password must be at least 8 characters')
    .regex(/[A-Z]/, 'Password must contain uppercase letter')
    .regex(/[a-z]/, 'Password must contain lowercase letter')
    .regex(/[0-9]/, 'Password must contain number')
});

export const updateUserSchema = z.object({
  displayName: z.string().min(1).max(100).optional()
});
```

### Role Validation

```typescript
export const createRoleSchema = z.object({
  name: z.string()
    .min(1, 'Name is required')
    .max(50)
    .regex(/^[a-z0-9-]+$/, 'Name must be lowercase alphanumeric with dashes'),
  displayName: z.string().min(1).max(100),
  description: z.string().max(500).optional(),
  permissions: z.array(z.string()).min(1, 'At least one permission required')
});
```

### Group Validation

```typescript
export const createGroupSchema = z.object({
  name: z.string().min(1, 'Name is required').max(100),
  description: z.string().max(500).optional()
});
```

---

## Type Guards

```typescript
/**
 * Type guard for checking if user has specific permission
 */
export function hasPermission(
  user: AuthenticatedUser | null,
  permission: string
): boolean {
  return user?.permissions?.includes(permission) ?? false;
}

/**
 * Type guard for API error
 */
export function isApiError(error: unknown): error is ApiError {
  return (
    typeof error === 'object' &&
    error !== null &&
    'status' in error &&
    'title' in error
  );
}
```

---

## Summary

This data model provides:

1. **Type Safety**: All API interactions are strongly typed
2. **Validation**: Zod schemas for runtime validation
3. **Enums**: Consistent status and type values
4. **Pagination**: Standardized pagination across all resources
5. **Error Handling**: Structured error responses
6. **Permission Checking**: Type-safe permission guards

**Next Steps**: Use these types in React Query hooks and API client layer.
