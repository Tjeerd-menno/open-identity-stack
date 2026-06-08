import { hasPermission as hasGrantedPermission } from '@openidentitystack/admin-api-client';

// ============================================================================
// Common Types
// ============================================================================

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
  page?: number;
  pageSize?: number;
  search?: string;
}

/**
 * Standard API error response
 */
export interface ApiError {
  type: string;
  title: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>;
  errorCode?: string;
}

/**
 * Result wrapper for API operations
 */
export type ApiResult<T> = 
  | { success: true; data: T }
  | { success: false; error: ApiError };

// ============================================================================
// User Management
// ============================================================================

export const UserStatus = {
  PendingVerification: 'PendingVerification',
  Active: 'Active',
  Disabled: 'Disabled',
  Locked: 'Locked'
} as const;
export type UserStatus = typeof UserStatus[keyof typeof UserStatus];

export interface User {
  id: string;
  email: string;
  displayName: string;
  status: UserStatus;
  mfaEnabled: boolean;
  lastLoginAt: string | null;
  createdAt: string;
  modifiedAt: string | null;
  profile: UserProfile;
}

export interface UserListItem {
  id: string;
  email: string;
  displayName: string;
  status: UserStatus;
  createdAt: string;
}

export interface CreateUserRequest {
  email: string;
  displayName: string;
  password: string;
  profile?: UserProfileRequest;
}

export interface UpdateUserRequest {
  displayName?: string;
  profile?: UserProfileRequest;
}

export interface UserProfile {
  givenName: string | null;
  familyName: string | null;
  middleName: string | null;
  nickname: string | null;
  preferredUsername: string | null;
  profile: string | null;
  picture: string | null;
  website: string | null;
  gender: string | null;
  birthdate: string | null;
  zoneInfo: string | null;
  locale: string | null;
}

export interface UserProfileRequest {
  givenName?: string | null;
  familyName?: string | null;
  middleName?: string | null;
  nickname?: string | null;
  preferredUsername?: string | null;
  profile?: string | null;
  picture?: string | null;
  website?: string | null;
  gender?: string | null;
  birthdate?: string | null;
  zoneInfo?: string | null;
  locale?: string | null;
}

export interface DisableUserRequest {
  reason: string;
}

export interface ResetPasswordRequest {
  newPassword: string;
}

export interface LinkUpstreamIdentityRequest {
  providerId: string;
  subjectId: string;
  email?: string;
}

export interface UpstreamIdentity {
  providerId: string;
  providerName: string;
  subjectId: string;
  email: string | null;
  linkedAt: string;
  lastLoginAt: string | null;
}

export interface UserStatusChangeResponse {
  userId: string;
  timestamp: string;
}

export interface PasswordResetResponse {
  userId: string;
  resetAt: string;
}

export interface LinkUpstreamIdentityResponse {
  userId: string;
  providerId: string;
  subjectId: string;
  linkedAt: string;
}

export type UserListResponse = PaginatedResponse<UserListItem>;

// ============================================================================
// Role Management
// ============================================================================

export interface Role {
  id: string;
  name: string;
  displayName: string;
  description: string | null;
  isSystemRole: boolean;
  isActive: boolean;
  permissions: string[];
}

export interface RoleListItem {
  id: string;
  name: string;
  displayName: string;
  isSystemRole: boolean;
  isActive: boolean;
}

export interface CreateRoleRequest {
  name: string;
  displayName: string;
  description?: string;
  permissions: string[];
  acknowledgeWildcardGrant?: boolean;
}

export interface UpdateRoleRequest {
  displayName?: string;
  description?: string;
  permissions?: string[];
  acknowledgeWildcardGrant?: boolean;
}

export type RoleListResponse = PaginatedResponse<RoleListItem>;

// ============================================================================
// Group Management
// ============================================================================

export const MappingType = {
  Role: 'Role',
  Claim: 'Claim'
} as const;
export type MappingType = typeof MappingType[keyof typeof MappingType];

export interface Group {
  id: string;
  name: string;
  description: string | null;
  memberCount: number;
  mappingCount: number;
  createdAt: string;
  modifiedAt: string | null;
}

export interface GroupListItem {
  id: string;
  name: string;
  description: string | null;
  memberCount: number;
  mappingCount: number;
  createdAt: string;
}

export interface GroupMember {
  userId: string;
  email: string;
  displayName: string;
  addedAt: string;
}

export interface GroupMapping {
  id: string;
  type: MappingType;
  value: string;
  createdAt: string;
}

export interface CreateGroupRequest {
  name: string;
  description?: string;
}

export interface UpdateGroupRequest {
  name?: string;
  description?: string;
}

export interface AddUserToGroupRequest {
  userId: string;
}

export interface AddGroupMappingRequest {
  type: MappingType;
  value: string;
}

export type GroupListResponse = PaginatedResponse<Group>;

// ============================================================================
// Service Account Management
// ============================================================================

export const ServiceAccountStatus = {
  Active: 'Active',
  Disabled: 'Disabled'
} as const;
export type ServiceAccountStatus = typeof ServiceAccountStatus[keyof typeof ServiceAccountStatus];

export interface ServiceAccount {
  id: string;
  clientId: string;
  displayName: string;
  status: ServiceAccountStatus;
  allowedScopes: string[];
  allowedGrantTypes: string[];
  credentialCount: number;
  certificateCount: number;
  createdAt: string;
  modifiedAt: string | null;
}

export interface CreateServiceAccountRequest {
  clientId: string;
  displayName: string;
  allowedScopes: string[];
  allowedGrantTypes: string[];
}

export interface UpdateServiceAccountRequest {
  displayName?: string;
  allowedScopes?: string[];
}

export interface RotateSecretRequest {
  revokeExisting?: boolean;
  description?: string;
  expiresAt?: string;
}

export interface AddCertificateRequest {
  thumbprint: string;
  subject: string;
  expiresAt: string;
}

export interface ServiceAccountCreatedResponse {
  id: string;
  clientId: string;
  displayName: string;
  initialSecret: string;
  createdAt: string;
}

export interface RotateSecretResponse {
  credentialId: string;
  newSecret: string;
}

export interface AddCertificateResponse {
  certificateId: string;
}

export interface Certificate {
  id: string;
  thumbprint: string;
  subject: string;
  issuedAt: string;
  expiresAt: string;
}

export interface ServiceAccountCredential {
  id: string;
  type: 'Secret' | 'Certificate';
  description?: string;
  issuedAt: string;
  expiresAt?: string;
}

export type ServiceAccountListResponse = PaginatedResponse<ServiceAccount>;

// ============================================================================
// Session Management
// ============================================================================

export const SessionStatus = {
  Active: 'Active',
  Expired: 'Expired',
  Revoked: 'Revoked',
  LoggedOut: 'LoggedOut'
} as const;
export type SessionStatus = typeof SessionStatus[keyof typeof SessionStatus];

export interface Session {
  id: string;
  userId: string;
  ipAddress: string;
  userAgent: string;
  status: SessionStatus;
  clientCount: number;
  lastActivityAt: string;
  expiresAt: string;
  createdAt: string;
}

export type SessionListResponse = PaginatedResponse<Session>;

export interface RevokeSessionResponse {
  sessionId: string;
  revokedAt: string;
}

export interface RevokeAllSessionsResponse {
  revokedCount: number;
  revokedAt: string;
}

// ============================================================================
// Provider Management (OIDC-only)
// ============================================================================

export const ProviderStatus = {
  Active: 'Active',
  Disabled: 'Disabled'
} as const;
export type ProviderStatus = typeof ProviderStatus[keyof typeof ProviderStatus];

export interface Provider {
  id: string;
  name: string;
  displayName: string;
  authority: string;
  clientId: string;
  scopes: string[];
  jitProvisioningEnabled: boolean;
  status: ProviderStatus;
  createdAt: string;
}

export interface CreateProviderRequest {
  name: string;
  displayName?: string;
  authority: string;
  clientId: string;
  clientSecret?: string;
  scopes?: string[];
  jitProvisioningEnabled?: boolean;
}

export interface UpdateProviderRequest {
  displayName?: string;
  clientId?: string;
  clientSecret?: string;
  scopes?: string[];
  jitProvisioningEnabled?: boolean;
  status?: ProviderStatus;
}

export type ProviderListResponse = Provider[];

// ============================================================================
// Authentication (Frontend-Only)
// ============================================================================

export interface AuthenticatedUser {
  sub: string;
  email: string;
  name: string;
  permissions: string[];
}

export interface AuthState {
  isAuthenticated: boolean;
  isLoading: boolean;
  user: AuthenticatedUser | null;
  accessToken: string | null;
  error: string | null;
}

// ============================================================================
// Type Guards
// ============================================================================

export function hasPermission(
  user: AuthenticatedUser | null,
  permission: string
): boolean {
  return user ? hasGrantedPermission(user.permissions, permission) : false;
}

export function isApiError(error: unknown): error is ApiError {
  return (
    typeof error === 'object' &&
    error !== null &&
    'status' in error &&
    'title' in error
  );
}

// ============================================================================
// Client Management
// ============================================================================

export const ClientType = {
  Confidential: 0,
  Public: 1,
} as const;
export type ClientType = typeof ClientType[keyof typeof ClientType];

export interface Client {
  id: string;
  clientIdValue: string;
  displayName: string;
  clientType: ClientType;
  description: string | null;
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  allowedScopes: string[];
  allowedGrantTypes: string[];
  requirePkce: boolean;
  requireConsent: boolean;
  createdAt: string;
  modifiedAt: string | null;
}

export interface ClientListItem {
  id: string;
  clientIdValue: string;
  displayName: string;
  clientType: ClientType;
  allowedGrantTypes: string[];
  createdAt: string;
}

export interface ClientListResponse {
  items: ClientListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CreateClientRequest {
  clientId: string;
  displayName: string;
  clientType: ClientType;
  description?: string | null;
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  allowedScopes: string[];
  allowedGrantTypes: string[];
  requirePkce: boolean;
  requireConsent: boolean;
}

export interface UpdateClientRequest {
  displayName: string;
  description?: string | null;
}

export interface ClientCreatedResponse {
  id: string;
  clientId: string;
  displayName: string;
  clientSecret: string | null;
  createdAt: string;
}

// ============================================================================
// Unified Application Management
// ============================================================================

export const ApplicationProfile = {
  Web: 'Web',
  SinglePage: 'SinglePage',
  Native: 'Native',
  MachineToMachine: 'MachineToMachine',
  Device: 'Device',
  Custom: 'Custom',
} as const;
export type ApplicationProfile = typeof ApplicationProfile[keyof typeof ApplicationProfile];

export const ApplicationClientType = {
  Confidential: 'Confidential',
  Public: 'Public',
} as const;
export type ApplicationClientType = typeof ApplicationClientType[keyof typeof ApplicationClientType];

export const ApplicationOptionAvailability = {
  Hidden: 'Hidden',
  ReadOnly: 'ReadOnly',
  Available: 'Available',
  Advanced: 'Advanced',
} as const;
export type ApplicationOptionAvailability =
  typeof ApplicationOptionAvailability[keyof typeof ApplicationOptionAvailability];

export type ApplicationStatus = 'Active' | 'Disabled';

export interface ApplicationProfilePolicy {
  applicationProfile: ApplicationProfile;
  isSelectable: boolean;
  unavailabilityReason: string | null;
  defaultClientProfile: ApplicationClientType;
  allowedClientProfiles: ApplicationClientType[];
  allowedGrantTypes: string[];
  defaultGrantTypes: string[];
  options: Record<string, ApplicationOptionAvailability>;
  requirePkce: boolean;
  defaultRequirePkce: boolean;
  defaultRequireConsent: boolean;
  requiresRedirectUris: boolean;
}

export interface Application {
  id: string;
  clientId: string;
  displayName: string;
  description: string | null;
  profile: ApplicationProfile;
  clientType: ApplicationClientType;
  status: ApplicationStatus;
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  allowedScopes: string[];
  allowedGrantTypes: string[];
  requirePkce: boolean;
  requireConsent: boolean;
  credentialCount: number;
  certificateCount: number;
  requiresMigrationReview: boolean;
  migrationSource: string | null;
  createdAt: string;
  modifiedAt: string | null;
}

export interface ApplicationListItem {
  id: string;
  clientId: string;
  displayName: string;
  profile: ApplicationProfile;
  clientType: ApplicationClientType;
  status: ApplicationStatus;
  allowedGrantTypes: string[];
  credentialCount: number;
  createdAt: string;
  modifiedAt: string | null;
}

export interface ApplicationListResponse {
  items: ApplicationListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
  hasPreviousPage?: boolean;
  hasNextPage?: boolean;
}

export interface ApplicationListParams extends PaginationParams {
  profile?: ApplicationProfile;
  status?: ApplicationStatus;
  clientType?: ApplicationClientType;
}

export interface CreateApplicationRequest {
  clientId: string;
  displayName: string;
  description?: string | null;
  profile: ApplicationProfile;
  clientType: ApplicationClientType;
  allowedGrantTypes: string[];
  allowedScopes: string[];
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  requirePkce: boolean;
  requireConsent: boolean;
}

export interface UpdateApplicationMetadataRequest {
  displayName: string;
  description?: string | null;
}

export type ConfigureApplicationOAuthRequest = Omit<
  CreateApplicationRequest,
  'clientId' | 'displayName' | 'description'
>;

export interface ApplicationCreatedResponse {
  id: string;
  clientId: string;
  displayName: string;
  profile: ApplicationProfile;
  clientType: ApplicationClientType;
  status: ApplicationStatus;
  initialSecret: string | null;
  createdAt: string;
}

export type ApplicationCredentialType = 'ClientSecret' | 'X509Certificate';

export interface ApplicationCredential {
  id: string;
  applicationId: string;
  type: ApplicationCredentialType;
  thumbprint: string | null;
  subject: string | null;
  description: string | null;
  expiresAt: string | null;
  createdAt: string;
  lastUsedAt: string | null;
  revokedAt: string | null;
}

export interface AddApplicationSecretRequest {
  description?: string | null;
  expiresAt?: string | null;
  revokeExisting: boolean;
}

export interface AddApplicationSecretResponse {
  credentialId: string;
  clientSecret: string;
}

export interface AddApplicationCertificateRequest {
  thumbprint: string;
  subject?: string | null;
  description?: string | null;
  expiresAt?: string | null;
}

export interface AddApplicationCertificateResponse {
  credentialId: string;
}

// ============================================================================
// Authentication Settings
// ============================================================================

export interface AuthenticationSettings {
  defaultProviderId: string;
  isLocalDefault: boolean;
  localFallbackEnabled: boolean;
  updatedAt: string;
}

export interface AuthenticationProvider {
  id: string;
  name: string;
  displayName: string;
  type: 'local' | 'external';
  isActive: boolean;
}

export interface SetDefaultProviderRequest {
  providerId: string;
}

export interface SetLocalFallbackRequest {
  enabled: boolean;
}

// ============================================================================
// Application Permission Registry
// ============================================================================

export interface ApplicationPermission {
  id: string;
  permissionKey: string;
  fullPermissionKey: string;
  displayName: string;
  description: string | null;
  category: string | null;
  createdAt: string;
  updatedAt: string | null;
  applicationId?: string | null;
  applicationName?: string | null;
  applicationVersion?: string | null;
  kind?: 'concrete' | 'wildcard';
  assignable?: boolean;
  resourceOrAggregate?: string | null;
  coveredPermissionCount?: number | null;
  isRemoved?: boolean;
  removedAt?: string | null;
  removedBy?: string | null;
  removeReason?: string | null;
  replacementFullPermissionKey?: string | null;
  replacementNote?: string | null;
  concurrencyToken?: number;
}

export interface RemovedPermissionDetail {
  id: string;
  applicationId: string;
  applicationIdentifier: string;
  permissionKey: string;
  fullPermissionKey: string;
  displayName: string;
  removedAt: string;
  removedBy: string;
  removeReason: string;
  replacementFullPermissionKey: string | null;
  replacementNote: string | null;
  concurrencyToken: number;
}

export interface DeletedApplicationHistory {
  id: string;
  applicationIdentifier: string;
  displayName: string;
  deletedAt: string;
  deletedBy: string;
  deleteReason: string;
}

export interface ApplicationPermissionHistory {
  deletedApplications: DeletedApplicationHistory[];
  removedPermissions: RemovedPermissionDetail[];
}

export interface PermissionDiagnosticIssue {
  kind: string;
  assignmentStore: string;
  assignmentId: string;
  assignmentDisplayName: string | null;
  permission: string;
  message: string;
}

export interface PermissionDiagnostics {
  issues: PermissionDiagnosticIssue[];
}

export interface ReplacementGuidanceRequest {
  replacementFullPermissionKey: string;
  replacementNote: string;
  concurrencyToken?: number;
}

export interface PlatformPermissionCatalogItem {
  permission: string;
  resource: string;
  action: string;
  kind: 'concrete' | 'wildcard';
  displayName: string;
  assignable: boolean;
}

export interface DelegatedMaintainer {
  id: string;
  principalId: string;
  principalType: string;
  grantedBy: string;
  grantedAt: string;
}

export interface RegisteredApplication {
  id: string;
  applicationIdentifier: string;
  displayName: string;
  description: string | null;
  schemaVersion: string;
  manifestVersion: string;
  manifestBaseUrl: string | null;
  ownerId: string;
  ownerType: string;
  status: string;
  createdAt: string;
  updatedAt: string | null;
  concurrencyToken: number;
  permissions: ApplicationPermission[];
  maintainers: DelegatedMaintainer[];
}

export interface PermissionAssignmentImpact {
  assignmentStore: string;
  assignmentId: string;
  assignmentDisplayName: string;
  permission: string;
  impactKind: string;
}

export interface DestructiveOperationResult {
  removedPermissions: ApplicationPermission[];
  exactAssignmentsRemoved: PermissionAssignmentImpact[];
  wildcardAssignmentsRemoved: PermissionAssignmentImpact[];
  wildcardAssignmentsImpacted: PermissionAssignmentImpact[];
  metadataUpdated: boolean;
  manifestVersionAdvanced: boolean;
  auditId?: string | null;
}

export interface ManifestPreview {
  applicationId: string;
  currentManifestVersion: string;
  requestedManifestVersion: string;
  hasChanges: boolean;
  versionWillAdvance: boolean;
  isDestructive: boolean;
  additions: ApplicationPermission[];
  metadataUpdates: ApplicationPermission[];
  removals: ApplicationPermission[];
  assignmentImpacts: PermissionAssignmentImpact[];
}

export interface ManifestApplyResult {
  application: RegisteredApplication;
  result: DestructiveOperationResult;
}

export interface RegisteredApplicationListItem {
  id: string;
  applicationIdentifier: string;
  displayName: string;
  ownerId: string;
  status: string;
  permissionCount: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface ApplicationPermissionInput {
  permissionKey: string;
  displayName: string;
  description?: string | null;
  category?: string | null;
}

export interface PermissionManifestApplication {
  id: string;
  displayName: string;
  description?: string | null;
  version: string;
}

export interface PermissionManifestPermission {
  key: string;
  displayName: string;
  description?: string | null;
  category?: string | null;
}

export interface PermissionManifestRequest {
  manifest: {
    schemaVersion: string;
    application: PermissionManifestApplication;
    permissions: PermissionManifestPermission[];
  };
  ownerId: string;
  ownerType: string;
  manifestBaseUrl?: string | null;
  acknowledgeRedeclare?: boolean;
}

export interface ImportPermissionManifestRequest {
  endpoint: string;
}

export interface UpdateRegisteredApplicationRequest {
  displayName: string;
  description?: string | null;
  concurrencyToken?: number;
  manifestBaseUrl?: string | null;
}

export interface AddApplicationPermissionRequest extends ApplicationPermissionInput {
  concurrencyToken?: number;
}

export interface ChangeLifecycleRequest {
  status: string;
  acknowledgeDependencies: boolean;
  concurrencyToken?: number;
}

export interface TransferApplicationOwnershipRequest {
  ownerId: string;
  ownerType: string;
  concurrencyToken?: number;
}

export interface AddDelegatedMaintainerRequest {
  principalId: string;
  principalType: string;
  concurrencyToken?: number;
}

export interface RoleAssignmentDependency {
  permissionKey: string;
  dependencyType: string;
  dependentId: string;
  dependentName: string;
  isActive: boolean;
  impact: string;
}

export type RegisteredApplicationListResponse = PaginatedResponse<RegisteredApplicationListItem>;
export type AssignablePermissionCatalogResponse = PaginatedResponse<ApplicationPermission>;
export interface PlatformPermissionCatalogResponse {
  items: PlatformPermissionCatalogItem[];
}
