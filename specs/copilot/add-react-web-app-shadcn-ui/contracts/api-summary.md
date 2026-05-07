# API Contracts Summary

**Date**: 2026-01-18  
**Purpose**: Document existing admin API endpoints consumed by the React admin web app.

---

## Authentication & Authorization

All endpoints require:
- **Authentication**: Bearer token (JWT) from OpenIddict server
- **Authorization Scheme**: `OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme`
- **Base URL**: `{API_BASE_URL}/api/admin` (e.g., `http://localhost:5000/api/admin`)

---

## 1. Users API (`/api/admin/users`)

### List Users
```http
GET /api/admin/users?page=1&pageSize=20&search=query
Authorization: Bearer {access_token}
Required Permission: users:read

Response 200: UserListResponse
{
  "items": [UserListItem],
  "totalCount": number,
  "page": number,
  "pageSize": number,
  "totalPages": number
}
```

### Create User
```http
POST /api/admin/users
Authorization: Bearer {access_token}
Required Permission: users:create
Content-Type: application/json

Request Body: CreateUserRequest
{
  "email": "user@example.com",
  "displayName": "John Doe",
  "password": "SecurePass123!"
}

Response 201: CreateUserResponse
{
  "id": "guid",
  "email": "user@example.com",
  "displayName": "John Doe",
  "status": "PendingVerification",
  "createdAt": "2026-01-18T12:00:00Z"
}
```

### Get User
```http
GET /api/admin/users/{id}
Authorization: Bearer {access_token}
Required Permission: users:read

Response 200: UserResponse
{
  "id": "guid",
  "email": "user@example.com",
  "displayName": "John Doe",
  "status": "Active",
  "mfaEnabled": false,
  "lastLoginAt": "2026-01-18T10:00:00Z",
  "createdAt": "2026-01-15T09:00:00Z",
  "modifiedAt": "2026-01-16T14:00:00Z"
}
```

### Update User
```http
PATCH /api/admin/users/{id}
Authorization: Bearer {access_token}
Required Permission: users:update
Content-Type: application/json

Request Body: UpdateUserRequest
{
  "displayName": "Jane Doe"
}

Response 200: UserResponse
```

### Disable User
```http
POST /api/admin/users/{id}/disable
Authorization: Bearer {access_token}
Required Permission: users:update
Content-Type: application/json

Request Body: DisableUserRequest
{
  "reason": "Suspicious activity"
}

Response 200: UserStatusChangeResponse
{
  "userId": "guid",
  "timestamp": "2026-01-18T12:00:00Z"
}
```

### Enable User
```http
POST /api/admin/users/{id}/enable
Authorization: Bearer {access_token}
Required Permission: users:update

Response 200: UserStatusChangeResponse
```

### Delete User
```http
DELETE /api/admin/users/{id}
Authorization: Bearer {access_token}
Required Permission: users:delete

Response 204: No Content
```

### Reset Password
```http
POST /api/admin/users/{id}/reset-password
Authorization: Bearer {access_token}
Required Permission: users:update
Content-Type: application/json

Request Body: ResetPasswordRequest
{
  "newPassword": "NewSecurePass456!"
}

Response 200: PasswordResetResponse
{
  "userId": "guid",
  "resetAt": "2026-01-18T12:00:00Z"
}
```

### Get User Roles
```http
GET /api/admin/users/{id}/roles
Authorization: Bearer {access_token}
Required Permission: users:read

Response 200: UserRolesResponse
{
  "userId": "guid",
  "roles": [Role]
}
```

### Assign Role to User
```http
POST /api/admin/users/{userId}/roles/{roleId}
Authorization: Bearer {access_token}
Required Permission: users:update

Response 204: No Content
```

### Unassign Role from User
```http
DELETE /api/admin/users/{userId}/roles/{roleId}
Authorization: Bearer {access_token}
Required Permission: users:update

Response 204: No Content
```

### Get User Groups
```http
GET /api/admin/users/{id}/groups
Authorization: Bearer {access_token}
Required Permission: users:read

Response 200: UserGroupsResponse
{
  "userId": "guid",
  "groups": [Group]
}
```

### Get User Upstream Identities
```http
GET /api/admin/users/{id}/upstream-identities
Authorization: Bearer {access_token}
Required Permission: users:read

Response 200: UpstreamIdentitiesResponse
{
  "items": [UpstreamIdentity]
}
```

### Link Upstream Identity
```http
POST /api/admin/users/{id}/upstream-identities
Authorization: Bearer {access_token}
Required Permission: users:update
Content-Type: application/json

Request Body: LinkUpstreamIdentityRequest
{
  "providerId": "guid",
  "subjectId": "external-user-id",
  "email": "user@provider.com"
}

Response 200: LinkUpstreamIdentityResponse
```

### Unlink Upstream Identity
```http
DELETE /api/admin/users/{userId}/upstream-identities/{providerId}
Authorization: Bearer {access_token}
Required Permission: users:update

Response 204: No Content
```

---

## 2. Roles API (`/api/admin/roles`)

### List Roles
```http
GET /api/admin/roles?page=1&pageSize=20
Authorization: Bearer {access_token}
Required Permission: roles:read

Response 200: RolesListResponse
{
  "items": [Role],
  "totalCount": number,
  "page": number,
  "pageSize": number
}
```

### Create Role
```http
POST /api/admin/roles
Authorization: Bearer {access_token}
Required Permission: roles:create
Content-Type: application/json

Request Body: CreateRoleRequest
{
  "name": "custom-admin",
  "displayName": "Custom Admin",
  "description": "Custom administrator role",
  "permissions": ["users:read", "users:create", "roles:read"]
}

Response 201: RoleResponse
```

### Get Role
```http
GET /api/admin/roles/{id}
Authorization: Bearer {access_token}
Required Permission: roles:read

Response 200: RoleResponse
{
  "id": "guid",
  "name": "custom-admin",
  "displayName": "Custom Admin",
  "description": "Custom administrator role",
  "isSystemRole": false,
  "isActive": true,
  "permissions": ["users:read", "users:create", "roles:read"]
}
```

### Update Role
```http
PATCH /api/admin/roles/{id}
Authorization: Bearer {access_token}
Required Permission: roles:update
Content-Type: application/json

Request Body: UpdateRoleRequest
{
  "displayName": "Updated Admin",
  "description": "Updated description",
  "permissions": ["users:read", "users:create", "users:update"]
}

Response 200: RoleResponse
```

### Delete Role
```http
DELETE /api/admin/roles/{id}
Authorization: Bearer {access_token}
Required Permission: roles:delete

Response 204: No Content
Note: System roles cannot be deleted
```

---

## 3. Groups API (`/api/admin/groups`)

### List Groups
```http
GET /api/admin/groups?page=1&pageSize=20&search=query
Authorization: Bearer {access_token}
Required Permission: groups:read

Response 200: GroupListResponse
```

### Create Group
```http
POST /api/admin/groups
Authorization: Bearer {access_token}
Required Permission: groups:create
Content-Type: application/json

Request Body: CreateGroupRequest
{
  "name": "Engineering Team",
  "description": "All engineering staff"
}

Response 201: Group
```

### Get Group
```http
GET /api/admin/groups/{id}
Authorization: Bearer {access_token}
Required Permission: groups:read

Response 200: Group
```

### Update Group
```http
PATCH /api/admin/groups/{id}
Authorization: Bearer {access_token}
Required Permission: groups:update
Content-Type: application/json

Request Body: UpdateGroupRequest
{
  "name": "Updated Engineering Team",
  "description": "Updated description"
}

Response 200: Group
```

### Delete Group
```http
DELETE /api/admin/groups/{id}
Authorization: Bearer {access_token}
Required Permission: groups:delete

Response 204: No Content
```

### Get Group Members
```http
GET /api/admin/groups/{id}/members?page=1&pageSize=20
Authorization: Bearer {access_token}
Required Permission: groups:read

Response 200: PaginatedResponse<GroupMember>
```

### Add User to Group
```http
POST /api/admin/groups/{groupId}/members/{userId}
Authorization: Bearer {access_token}
Required Permission: groups:update

Response 204: No Content
```

### Remove User from Group
```http
DELETE /api/admin/groups/{groupId}/members/{userId}
Authorization: Bearer {access_token}
Required Permission: groups:update

Response 204: No Content
```

### Get Group Mappings (Role/Claim)
```http
GET /api/admin/groups/{id}/mappings
Authorization: Bearer {access_token}
Required Permission: groups:read

Response 200: { items: [GroupMapping] }
```

### Add Group Mapping
```http
POST /api/admin/groups/{id}/mappings
Authorization: Bearer {access_token}
Required Permission: groups:update
Content-Type: application/json

Request Body: AddGroupMappingRequest
{
  "type": "Role",
  "value": "role-guid"
}

Response 201: GroupMapping
```

### Remove Group Mapping
```http
DELETE /api/admin/groups/{groupId}/mappings/{mappingId}
Authorization: Bearer {access_token}
Required Permission: groups:update

Response 204: No Content
```

---

## 4. Service Accounts API (`/api/admin/service-accounts`)

### List Service Accounts
```http
GET /api/admin/service-accounts?page=1&pageSize=20
Authorization: Bearer {access_token}
Required Permission: service-accounts:read

Response 200: ListServiceAccountsResponse
```

### Create Service Account
```http
POST /api/admin/service-accounts
Authorization: Bearer {access_token}
Required Permission: service-accounts:create
Content-Type: application/json

Request Body: CreateServiceAccountRequest
{
  "clientId": "my-service-app",
  "displayName": "My Service Application",
  "allowedScopes": ["admin-api", "openid"],
  "allowedGrantTypes": ["client_credentials"]
}

Response 201: ServiceAccountCreatedResponse
{
  "id": "guid",
  "clientId": "my-service-app",
  "displayName": "My Service Application",
  "initialSecret": "generated-secret-ONCE",
  "createdAt": "2026-01-18T12:00:00Z"
}
Note: initialSecret is only returned once!
```

### Get Service Account
```http
GET /api/admin/service-accounts/{id}
Authorization: Bearer {access_token}
Required Permission: service-accounts:read

Response 200: ServiceAccountResponse
```

### Update Service Account
```http
PATCH /api/admin/service-accounts/{id}
Authorization: Bearer {access_token}
Required Permission: service-accounts:update
Content-Type: application/json

Request Body: UpdateServiceAccountRequest
{
  "displayName": "Updated Service App",
  "allowedScopes": ["admin-api", "openid", "profile"]
}

Response 200: ServiceAccountResponse
```

### Enable Service Account
```http
POST /api/admin/service-accounts/{id}/enable
Authorization: Bearer {access_token}
Required Permission: service-accounts:update

Response 204: No Content
```

### Disable Service Account
```http
POST /api/admin/service-accounts/{id}/disable
Authorization: Bearer {access_token}
Required Permission: service-accounts:update

Response 204: No Content
```

### Delete Service Account
```http
DELETE /api/admin/service-accounts/{id}
Authorization: Bearer {access_token}
Required Permission: service-accounts:delete

Response 204: No Content
```

### Rotate Secret
```http
POST /api/admin/service-accounts/{id}/rotate-secret
Authorization: Bearer {access_token}
Required Permission: service-accounts:update
Content-Type: application/json

Request Body: RotateSecretRequest
{
  "revokeExisting": true,
  "description": "Monthly rotation",
  "expiresAt": "2026-02-18T12:00:00Z"
}

Response 200: RotateSecretResponse
{
  "credentialId": "guid",
  "newSecret": "new-generated-secret-ONCE"
}
Note: newSecret is only returned once!
```

### Add Certificate
```http
POST /api/admin/service-accounts/{id}/certificates
Authorization: Bearer {access_token}
Required Permission: service-accounts:update
Content-Type: application/json

Request Body: AddCertificateRequest
{
  "thumbprint": "cert-thumbprint",
  "subject": "CN=My Service",
  "expiresAt": "2027-01-18T12:00:00Z"
}

Response 200: AddCertificateResponse
{
  "certificateId": "guid"
}
```

---

## 5. Sessions API (`/api/admin/sessions`)

### List Sessions
```http
GET /api/admin/sessions?page=1&pageSize=20
Authorization: Bearer {access_token}
Required Permission: sessions:read

Response 200: SessionListResponse
```

### Get Session
```http
GET /api/admin/sessions/{id}
Authorization: Bearer {access_token}
Required Permission: sessions:read

Response 200: SessionResponse
{
  "id": "guid",
  "userId": "user-guid",
  "ipAddress": "192.168.1.100",
  "userAgent": "Mozilla/5.0...",
  "status": "Active",
  "clientCount": 2,
  "lastActivityAt": "2026-01-18T11:55:00Z",
  "expiresAt": "2026-01-18T18:00:00Z",
  "createdAt": "2026-01-18T08:00:00Z"
}
```

### Revoke Session
```http
DELETE /api/admin/sessions/{id}
Authorization: Bearer {access_token}
Required Permission: sessions:revoke

Response 200: RevokeSessionResponse
{
  "sessionId": "guid",
  "revokedAt": "2026-01-18T12:00:00Z"
}
```

### Revoke All User Sessions
```http
DELETE /api/admin/users/{userId}/sessions
Authorization: Bearer {access_token}
Required Permission: sessions:revoke

Response 200: RevokeAllSessionsResponse
{
  "revokedCount": 3,
  "revokedAt": "2026-01-18T12:00:00Z"
}
```

### Logout Session (Alternative Route)
```http
POST /api/admin/sessions/{sessionId}/logout
Authorization: Bearer {access_token}
Required Permission: sessions:revoke

Response 204: No Content
```

---

## 6. Providers API (`/api/admin/providers`)

### List Providers
```http
GET /api/admin/providers?page=1&pageSize=20
Authorization: Bearer {access_token}
Required Permission: providers:read

Response 200: ProviderListResponse
```

### Create Provider
```http
POST /api/admin/providers
Authorization: Bearer {access_token}
Required Permission: providers:create
Content-Type: application/json

Request Body: CreateProviderRequest
{
  "name": "google",
  "displayName": "Google",
  "type": "OIDC",
  "enabled": true,
  "authority": "https://accounts.google.com",
  "clientId": "google-client-id",
  "clientSecret": "google-client-secret",
  "metadataUrl": "https://accounts.google.com/.well-known/openid-configuration",
  "configuration": {
    "scope": "openid profile email"
  }
}

Response 201: Provider
```

### Get Provider
```http
GET /api/admin/providers/{id}
Authorization: Bearer {access_token}
Required Permission: providers:read

Response 200: Provider
{
  "id": "guid",
  "name": "google",
  "displayName": "Google",
  "type": "OIDC",
  "enabled": true,
  "authority": "https://accounts.google.com",
  "clientId": "google-client-id",
  "metadataUrl": "https://accounts.google.com/.well-known/openid-configuration",
  "configuration": { ... },
  "createdAt": "2026-01-15T10:00:00Z",
  "modifiedAt": "2026-01-16T14:00:00Z"
}
Note: clientSecret is never returned
```

### Update Provider
```http
PATCH /api/admin/providers/{id}
Authorization: Bearer {access_token}
Required Permission: providers:update
Content-Type: application/json

Request Body: UpdateProviderRequest
{
  "displayName": "Google OAuth",
  "enabled": false,
  "configuration": {
    "scope": "openid profile email phone"
  }
}

Response 200: Provider
```

### Delete Provider
```http
DELETE /api/admin/providers/{id}
Authorization: Bearer {access_token}
Required Permission: providers:delete

Response 204: No Content
```

---

## Permission Reference

| Resource | Permissions |
|----------|-------------|
| **Users** | `users:read`, `users:create`, `users:update`, `users:delete` |
| **Roles** | `roles:read`, `roles:create`, `roles:update`, `roles:delete` |
| **Groups** | `groups:read`, `groups:create`, `groups:update`, `groups:delete` |
| **Service Accounts** | `service-accounts:read`, `service-accounts:create`, `service-accounts:update`, `service-accounts:delete` |
| **Sessions** | `sessions:read`, `sessions:revoke` |
| **Providers** | `providers:read`, `providers:create`, `providers:update`, `providers:delete` |

**Super Admin**: User with all permissions above.

---

## Error Responses

All endpoints may return:

### 400 Bad Request
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["The Email field is required."],
    "Password": ["Password must be at least 8 characters."]
  }
}
```

### 401 Unauthorized
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Access token is missing or invalid."
}
```

### 403 Forbidden
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to perform this action."
}
```

### 404 Not Found
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "The requested resource was not found."
}
```

### 409 Conflict
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "A user with this email already exists."
}
```

### 500 Internal Server Error
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "An error occurred while processing your request.",
  "status": 500
}
```

---

## CORS Configuration

The API must be configured to allow requests from the admin web app origin:

```csharp
// In API Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminWeb", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "https://localhost:5174")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

app.UseCors("AdminWeb");
```

---

## Rate Limiting

Admin API endpoints may implement rate limiting:
- **Limit**: 100 requests per minute per user
- **Response Header**: `X-RateLimit-Remaining`, `X-RateLimit-Reset`
- **429 Status**: Too Many Requests

---

## Summary

This document provides a comprehensive reference for all admin API endpoints that the React admin web app will consume. All endpoints are protected by OpenIddict JWT validation and require specific permissions.

**Total Endpoints**: ~45 endpoints across 6 resource groups  
**Authentication**: OAuth2/OIDC Bearer tokens  
**Authorization**: Permission-based access control
