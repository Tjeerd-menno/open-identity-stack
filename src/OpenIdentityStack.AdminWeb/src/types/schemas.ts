import { z } from 'zod';

// ============================================================================
// User Validation Schemas
// ============================================================================

export const createUserSchema = z.object({
  email: z.string().email('Invalid email address'),
  displayName: z.string().min(1, 'Display name is required').max(100),
  password: z
    .string()
    .min(8, 'Password must be at least 8 characters')
    .regex(/[A-Z]/, 'Password must contain at least one uppercase letter')
    .regex(/[a-z]/, 'Password must contain at least one lowercase letter')
    .regex(/[0-9]/, 'Password must contain at least one number'),
});

export const updateUserSchema = z.object({
  displayName: z.string().min(1).max(100).optional(),
});

export const disableUserSchema = z.object({
  reason: z.string().min(1, 'Reason is required').max(500),
});

export const resetPasswordSchema = z.object({
  newPassword: z
    .string()
    .min(8, 'Password must be at least 8 characters')
    .regex(/[A-Z]/, 'Password must contain at least one uppercase letter')
    .regex(/[a-z]/, 'Password must contain at least one lowercase letter')
    .regex(/[0-9]/, 'Password must contain at least one number'),
});

export const linkUpstreamIdentitySchema = z.object({
  providerId: z.string().uuid('Invalid provider ID'),
  subjectId: z.string().min(1, 'Subject ID is required'),
  email: z.string().email().optional(),
});

// ============================================================================
// Role Validation Schemas
// ============================================================================

export const createRoleSchema = z.object({
  name: z
    .string()
    .min(1, 'Name is required')
    .max(50)
    .regex(/^[a-z0-9-]+$/, 'Name must be lowercase alphanumeric with dashes'),
  displayName: z.string().min(1, 'Display name is required').max(100),
  description: z.string().max(500).optional(),
  permissions: z.array(z.string()).min(1, 'At least one permission is required'),
});

export const updateRoleSchema = z.object({
  displayName: z.string().min(1).max(100).optional(),
  description: z.string().max(500).optional(),
  permissions: z.array(z.string()).min(1).optional(),
});

// ============================================================================
// Group Validation Schemas
// ============================================================================

export const createGroupSchema = z.object({
  name: z.string().min(1, 'Name is required').max(100),
  description: z.string().max(500).optional(),
});

export const updateGroupSchema = z.object({
  name: z.string().min(1).max(100).optional(),
  description: z.string().max(500).optional(),
});

export const addUserToGroupSchema = z.object({
  userId: z.string().uuid('Invalid user ID'),
});

export const addGroupMappingSchema = z.object({
  type: z.enum(['Role', 'Claim']),
  value: z.string().min(1, 'Value is required'),
});

// ============================================================================
// Service Account Validation Schemas
// ============================================================================

export const createServiceAccountSchema = z.object({
  clientId: z.string().min(1, 'Client ID is required').max(100),
  displayName: z.string().min(1, 'Display name is required').max(100),
  allowedScopes: z.array(z.string()).min(1, 'At least one scope is required'),
  allowedGrantTypes: z.array(z.string()).min(1, 'At least one grant type is required'),
});

export const updateServiceAccountSchema = z.object({
  displayName: z.string().min(1).max(100).optional(),
  allowedScopes: z.array(z.string()).min(1).optional(),
});

export const rotateSecretSchema = z.object({
  revokeExisting: z.boolean().optional(),
  description: z.string().max(200).optional(),
  expiresAt: z.string().optional(), // Changed from datetime() which needs format param
});

export const addCertificateSchema = z.object({
  thumbprint: z.string().min(1, 'Thumbprint is required'),
  subject: z.string().min(1, 'Subject is required'),
  expiresAt: z.string().min(1, 'Expiration date is required'), // Changed from datetime()
});

// ============================================================================
// Provider Validation Schemas
// ============================================================================

export const createProviderSchema = z.object({
  name: z
    .string()
    .min(1, 'Name is required')
    .max(50)
    .regex(/^[a-z0-9-]+$/, 'Name must be lowercase alphanumeric with dashes'),
  displayName: z.string().min(1, 'Display name is required').max(100),
  type: z.enum(['OIDC', 'OAuth2', 'SAML2']),
  enabled: z.boolean().optional(),
  authority: z.string().url('Invalid authority URL').optional(),
  clientId: z.string().optional(),
  clientSecret: z.string().optional(),
  metadataUrl: z.string().url('Invalid metadata URL').optional(),
  configuration: z.record(z.string(), z.unknown()).optional(),
});

export const updateProviderSchema = z.object({
  displayName: z.string().min(1).max(100).optional(),
  enabled: z.boolean().optional(),
  authority: z.string().url().optional(),
  clientId: z.string().optional(),
  clientSecret: z.string().optional(),
  metadataUrl: z.string().url().optional(),
  configuration: z.record(z.string(), z.unknown()).optional(),
});

// ============================================================================
// Pagination Validation
// ============================================================================

export const paginationSchema = z.object({
  page: z.number().int().positive().optional(),
  pageSize: z.number().int().positive().max(100).optional(),
  search: z.string().optional(),
});
