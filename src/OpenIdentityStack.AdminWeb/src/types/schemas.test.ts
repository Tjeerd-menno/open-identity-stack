import { describe, expect, it } from 'vitest';
import {
  addCertificateSchema,
  addGroupMappingSchema,
  createProviderSchema,
  createRoleSchema,
  createServiceAccountSchema,
  createUserSchema,
  paginationSchema,
  resetPasswordSchema,
} from './schemas';

describe('validation schemas', () => {
  it('accepts strong user creation data and rejects weak passwords', () => {
    expect(
      createUserSchema.safeParse({
        email: 'alice@example.com',
        displayName: 'Alice',
        password: 'Password1',
      }).success
    ).toBe(true);

    const result = createUserSchema.safeParse({
      email: 'not-email',
      displayName: '',
      password: 'weak',
    });

    expect(result.success).toBe(false);
    expect(result.error?.issues.map((issue) => issue.message)).toEqual(
      expect.arrayContaining([
        'Invalid email address',
        'Display name is required',
        'Password must be at least 8 characters',
      ])
    );
  });

  it('uses the same password policy for resets', () => {
    expect(resetPasswordSchema.safeParse({ newPassword: 'ResetPass1' }).success).toBe(true);
    expect(resetPasswordSchema.safeParse({ newPassword: 'resetpass' }).success).toBe(false);
  });

  it('validates role names and requires at least one permission', () => {
    expect(
      createRoleSchema.safeParse({
        name: 'service-admin',
        displayName: 'Service Admin',
        permissions: ['service-accounts:read'],
      }).success
    ).toBe(true);

    expect(
      createRoleSchema.safeParse({
        name: 'Service Admin',
        displayName: 'Service Admin',
        permissions: [],
      }).success
    ).toBe(false);
  });

  it('validates group mappings by type and value', () => {
    expect(addGroupMappingSchema.safeParse({ type: 'Role', value: 'admin' }).success).toBe(true);
    expect(addGroupMappingSchema.safeParse({ type: 'Unknown', value: 'admin' }).success).toBe(false);
    expect(addGroupMappingSchema.safeParse({ type: 'Claim', value: '' }).success).toBe(false);
  });

  it('requires service account scopes and grant types', () => {
    expect(
      createServiceAccountSchema.safeParse({
        clientId: 'worker',
        displayName: 'Worker',
        allowedScopes: ['api'],
        allowedGrantTypes: ['client_credentials'],
      }).success
    ).toBe(true);

    expect(
      createServiceAccountSchema.safeParse({
        clientId: 'worker',
        displayName: 'Worker',
        allowedScopes: [],
        allowedGrantTypes: [],
      }).success
    ).toBe(false);
  });

  it('validates certificate fields', () => {
    expect(
      addCertificateSchema.safeParse({
        thumbprint: 'abc',
        subject: 'CN=worker',
        expiresAt: '2027-01-01',
      }).success
    ).toBe(true);
    expect(addCertificateSchema.safeParse({ thumbprint: '', subject: '', expiresAt: '' }).success).toBe(false);
  });

  it('validates provider names and URLs', () => {
    expect(
      createProviderSchema.safeParse({
        name: 'google',
        displayName: 'Google',
        type: 'OIDC',
        authority: 'https://accounts.google.com',
      }).success
    ).toBe(true);

    expect(
      createProviderSchema.safeParse({
        name: 'Google Provider',
        displayName: 'Google',
        type: 'OIDC',
        authority: 'not-a-url',
      }).success
    ).toBe(false);
  });

  it('bounds pagination values', () => {
    expect(paginationSchema.safeParse({ page: 1, pageSize: 100, search: 'alice' }).success).toBe(true);
    expect(paginationSchema.safeParse({ page: 0, pageSize: 101 }).success).toBe(false);
  });
});
