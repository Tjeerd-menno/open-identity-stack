import { describe, expect, it, vi } from 'vitest';
import type { AdminApiClient } from './index';
import { createApplicationPermissionsContract, type PermissionManifestRequest } from './application-permissions';
import { createApplicationsContract, type CreateApplicationRequest } from './applications';
import { createAuditEntriesContract } from './audit-entries';
import { createGroupsContract } from './groups';
import { createProvidersContract } from './providers';
import { createRolesContract } from './roles';
import { createSessionsContract } from './sessions';
import { createSettingsContract } from './settings';
import { createUsersContract } from './users';

type MockClient = {
  get: ReturnType<typeof vi.fn>;
  post: ReturnType<typeof vi.fn>;
  put: ReturnType<typeof vi.fn>;
  patch: ReturnType<typeof vi.fn>;
  delete: ReturnType<typeof vi.fn>;
};

function createMockClient(): MockClient {
  return {
    get: vi.fn().mockResolvedValue({ items: [] }),
    post: vi.fn().mockResolvedValue({}),
    put: vi.fn().mockResolvedValue({}),
    patch: vi.fn().mockResolvedValue({}),
    delete: vi.fn().mockResolvedValue(undefined),
  };
}

function asAdminApiClient(client: MockClient): AdminApiClient {
  return client as unknown as AdminApiClient;
}

function expectCalls(mock: ReturnType<typeof vi.fn>, calls: readonly unknown[][]): void {
  expect(mock.mock.calls).toEqual(calls);
}

describe('Admin API domain contracts', () => {
  it('maps application lifecycle and credential operations to their routes', async () => {
    const client = createMockClient();
    const contract = createApplicationsContract(asAdminApiClient(client));
    const application = {
      clientId: 'web',
      displayName: 'Web',
      profile: 'Web',
      clientType: 'Public',
      allowedGrantTypes: [],
      allowedScopes: [],
      redirectUris: [],
      postLogoutRedirectUris: [],
      requirePkce: true,
      requireConsent: false,
    } satisfies CreateApplicationRequest;

    await contract.getApplications({ page: 2, search: 'web' });
    await contract.getApplication('app-1');
    await contract.getApplicationProfilePolicies();
    await contract.createApplication(application);
    await contract.updateApplicationMetadata('app-1', { displayName: 'Updated' });
    await contract.configureApplicationOAuth('app-1', application);
    await contract.disableApplication('app-1');
    await contract.enableApplication('app-1');
    await contract.deleteApplication('app-1');
    await contract.listApplicationCredentials('app-1');
    await contract.addApplicationSecret('app-1', { revokeExisting: true });
    await contract.addApplicationCertificate('app-1', { thumbprint: 'thumbprint' });
    await contract.revokeApplicationCredential('app-1', 'credential-1');

    expectCalls(client.get, [
      ['/api/admin/applications', { page: 2, search: 'web' }],
      ['/api/admin/applications/app-1'],
      ['/api/admin/applications/policies/profiles'],
      ['/api/admin/applications/app-1/credentials'],
    ]);
    expectCalls(client.post, [
      ['/api/admin/applications', application],
      ['/api/admin/applications/app-1/disable'],
      ['/api/admin/applications/app-1/enable'],
      ['/api/admin/applications/app-1/credentials/client-secrets', { revokeExisting: true }],
      ['/api/admin/applications/app-1/credentials/certificates', { thumbprint: 'thumbprint' }],
    ]);
    expectCalls(client.put, [['/api/admin/applications/app-1/oauth', application]]);
    expectCalls(client.patch, [['/api/admin/applications/app-1', { displayName: 'Updated' }]]);
    expectCalls(client.delete, [
      ['/api/admin/applications/app-1'],
      ['/api/admin/applications/app-1/credentials/credential-1'],
    ]);
  });

  it('normalizes application-permission mutations and encodes maintainer query parameters', async () => {
    const client = createMockClient();
    client.post
      .mockResolvedValueOnce({ applicationId: 'registered-1' })
      .mockResolvedValueOnce({ id: 'registered-2' });
    const contract = createApplicationPermissionsContract(asAdminApiClient(client));
    const manifest = {
      manifest: {
        schemaVersion: '1.0.0',
        application: { id: 'inventory', displayName: 'Inventory', version: '1.0.0' },
        permissions: [{ key: 'inventory:read', displayName: 'Read inventory' }],
      },
      ownerId: 'owner-1',
      ownerType: 'User',
    } satisfies PermissionManifestRequest;

    await expect(contract.registerPermissionManifest(manifest)).resolves.toEqual({ id: 'registered-1' });
    await expect(contract.importPermissionManifestFromEndpoint('https://inventory.example/manifest')).resolves.toEqual({ id: 'registered-2' });
    await contract.getRegisteredApplications({ search: 'inventory' });
    await contract.getRegisteredApplication('registered-1');
    await contract.previewPermissionManifest('registered-1', manifest, 4);
    await contract.applyPermissionManifest('registered-1', manifest, 4);
    await contract.previewRemotePermissionManifest('registered-1', 5);
    await contract.applyRemotePermissionManifest('registered-1', 5);
    await contract.updateRegisteredApplication('registered-1', { displayName: 'Updated' });
    await contract.addApplicationPermission('registered-1', { permissionKey: 'inventory:write', displayName: 'Write inventory' });
    await contract.changeApplicationLifecycle('registered-1', { status: 'Disabled', acknowledgeDependencies: true });
    await contract.transferApplicationOwnership('registered-1', { ownerId: 'owner-2', ownerType: 'Group' });
    await contract.addDelegatedMaintainer('registered-1', { principalId: 'user/2', principalType: 'User' });
    await contract.removeDelegatedMaintainer('registered-1', 'user/2', 7);
    await contract.getAssignablePermissionCatalog({ page: 2 });
    await contract.getApplicationPermissionHistory({ applicationIdentifier: 'inventory' });
    await contract.getApplicationPermissionDiagnostics();
    await contract.updateRemovedPermissionReplacement('permission-1', { replacementFullPermissionKey: 'inventory:read' });
    await contract.getPermissionDependencies('permission-1');

    expectCalls(client.get, [
      ['/api/admin/application-permissions/applications', { search: 'inventory' }],
      ['/api/admin/application-permissions/applications/registered-1'],
      ['/api/admin/application-permissions/catalog', { page: 2 }],
      ['/api/admin/application-permissions/history', { applicationIdentifier: 'inventory' }],
      ['/api/admin/application-permissions/diagnostics'],
      ['/api/admin/application-permissions/permissions/permission-1/dependencies'],
    ]);
    expectCalls(client.post, [
      ['/api/admin/application-permissions/applications', manifest],
      ['/api/admin/application-permissions/applications/import', { endpoint: 'https://inventory.example/manifest' }],
      ['/api/admin/application-permissions/applications/registered-1/manifest/preview', {
        manifest: manifest.manifest,
        concurrencyToken: 4,
      }],
      ['/api/admin/application-permissions/applications/registered-1/manifest', {
        manifest: manifest.manifest,
        concurrencyToken: 4,
      }],
      ['/api/admin/application-permissions/applications/registered-1/import/preview', { concurrencyToken: 5 }],
      ['/api/admin/application-permissions/applications/registered-1/import', { concurrencyToken: 5 }],
      ['/api/admin/application-permissions/applications/registered-1/permissions', {
        permissionKey: 'inventory:write',
        displayName: 'Write inventory',
      }],
      ['/api/admin/application-permissions/applications/registered-1/lifecycle', {
        status: 'Disabled',
        acknowledgeDependencies: true,
      }],
      ['/api/admin/application-permissions/applications/registered-1/ownership', {
        ownerId: 'owner-2',
        ownerType: 'Group',
      }],
      ['/api/admin/application-permissions/applications/registered-1/maintainers', {
        principalId: 'user/2',
        principalType: 'User',
      }],
    ]);
    expectCalls(client.patch, [
      ['/api/admin/application-permissions/applications/registered-1', { displayName: 'Updated' }],
      ['/api/admin/application-permissions/permissions/permission-1/replacement', {
        replacementFullPermissionKey: 'inventory:read',
      }],
    ]);
    expectCalls(client.delete, [
      ['/api/admin/application-permissions/applications/registered-1/maintainers/user%2F2?concurrencyToken=7'],
    ]);
  });

  it('normalizes group member identifiers while mapping group operations', async () => {
    const client = createMockClient();
    client.get.mockImplementation((path: string) =>
      Promise.resolve(
        path.endsWith('/members')
          ? {
              items: [{ id: 'user-1', email: 'ada@example.com', displayName: 'Ada' }],
              nextPageToken: 'next-page',
            }
          : { items: [] }
      )
    );
    const contract = createGroupsContract(asAdminApiClient(client));

    await contract.getGroups({ page: 1 });
    await contract.getGroup('group-1');
    await contract.createGroup({ name: 'Operators' });
    await contract.updateGroup('group-1', { name: 'Admins' });
    await contract.deleteGroup('group-1');
    const members = await contract.getGroupMembers('group-1', { pageSize: 10 });
    await contract.addMemberToGroup('group-1', 'user-1');
    await contract.removeMemberFromGroup('group-1', 'user-1');
    await contract.getGroupMappings('group-1');
    await contract.addGroupMapping('group-1', { type: 'Role', value: 'operator' });
    await contract.removeGroupMapping('group-1', 'mapping-1');

    expect(members).toEqual({
      items: [{ id: 'user-1', userId: 'user-1', email: 'ada@example.com', displayName: 'Ada' }],
      nextPageToken: 'next-page',
    });
    expectCalls(client.get, [
      ['/api/admin/groups', { page: 1 }],
      ['/api/admin/groups/group-1'],
      ['/api/admin/groups/group-1/members', { pageSize: 10 }],
      ['/api/admin/groups/group-1/mappings'],
    ]);
    expectCalls(client.post, [
      ['/api/admin/groups', { name: 'Operators' }],
      ['/api/admin/groups/group-1/members/user-1'],
      ['/api/admin/groups/group-1/mappings', { type: 'Role', value: 'operator' }],
    ]);
    expectCalls(client.patch, [['/api/admin/groups/group-1', { name: 'Admins' }]]);
    expectCalls(client.delete, [
      ['/api/admin/groups/group-1'],
      ['/api/admin/groups/group-1/members/user-1'],
      ['/api/admin/groups/group-1/mappings/mapping-1'],
    ]);
  });

  it('normalizes both supported user role and upstream identity response shapes', async () => {
    const client = createMockClient();
    client.get
      .mockResolvedValueOnce({ items: [] })
      .mockResolvedValueOnce({})
      .mockResolvedValueOnce({ userId: 'user-1', roles: [{ id: 'role-1', name: 'operator' }] })
      .mockResolvedValueOnce([{ id: 'role-2', name: 'auditor' }])
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce({ items: [{ providerId: 'provider-1', subject: 'sub-1' }] })
      .mockResolvedValueOnce([{ providerId: 'provider-2', subject: 'sub-2' }]);
    const contract = createUsersContract(asAdminApiClient(client));

    await contract.getUsers({ search: 'ada' });
    await contract.getUser('user-1');
    await contract.createUser({ email: 'ada@example.com', displayName: 'Ada', password: 'Password123!' });
    await contract.updateUser('user-1', { displayName: 'Ada Lovelace' });
    await contract.disableUser('user-1', { reason: 'operator request' });
    await contract.enableUser('user-1');
    await contract.deleteUser('user-1');
    await contract.resetUserPassword('user-1', { newPassword: 'NewPassword123!' });
    await expect(contract.getUserRoles('user-1')).resolves.toEqual([{ id: 'role-1', name: 'operator' }]);
    await expect(contract.getUserRoles('user-1')).resolves.toEqual([{ id: 'role-2', name: 'auditor' }]);
    await contract.assignUserRole('user-1', 'role-1');
    await contract.unassignUserRole('user-1', 'role-1');
    await contract.getUserGroups('user-1');
    await expect(contract.getUserUpstreamIdentities('user-1')).resolves.toEqual([{ providerId: 'provider-1', subject: 'sub-1' }]);
    await expect(contract.getUserUpstreamIdentities('user-1')).resolves.toEqual([{ providerId: 'provider-2', subject: 'sub-2' }]);
    expect(contract).not.toHaveProperty('linkUserUpstreamIdentity');
    await contract.unlinkUserUpstreamIdentity('user-1', 'provider-1');

    expectCalls(client.get, [
      ['/api/admin/users', { search: 'ada' }],
      ['/api/admin/users/user-1'],
      ['/api/admin/users/user-1/roles'],
      ['/api/admin/users/user-1/roles'],
      ['/api/admin/users/user-1/groups'],
      ['/api/admin/users/user-1/upstream-identities'],
      ['/api/admin/users/user-1/upstream-identities'],
    ]);
    expectCalls(client.post, [
      ['/api/admin/users', { email: 'ada@example.com', displayName: 'Ada', password: 'Password123!' }],
      ['/api/admin/users/user-1/disable', { reason: 'operator request' }],
      ['/api/admin/users/user-1/enable'],
      ['/api/admin/users/user-1/reset-password', { newPassword: 'NewPassword123!' }],
      ['/api/admin/users/user-1/roles/role-1'],
    ]);
    expectCalls(client.put, [['/api/admin/users/user-1', { displayName: 'Ada Lovelace' }]]);
    expectCalls(client.delete, [
      ['/api/admin/users/user-1'],
      ['/api/admin/users/user-1/roles/role-1'],
      ['/api/admin/users/user-1/upstream-identities/provider-1'],
    ]);
  });

  it('maps provider, role, session, settings, and audit routes', async () => {
    const client = createMockClient();
    const providers = createProvidersContract(asAdminApiClient(client));
    const roles = createRolesContract(asAdminApiClient(client));
    const sessions = createSessionsContract(asAdminApiClient(client));
    const settings = createSettingsContract(asAdminApiClient(client));
    const audit = createAuditEntriesContract(asAdminApiClient(client));

    await providers.getProviders(true);
    await providers.getProvider('provider-1');
    await providers.createProvider({ name: 'github', authority: 'https://github.com', clientId: 'client-1' });
    await providers.updateProvider('provider-1', { displayName: 'GitHub' });
    await providers.enableProvider('provider-1');
    await providers.disableProvider('provider-1');
    await providers.deleteProvider('provider-1');
    await roles.getRoles({ search: 'operator' });
    await roles.getRole('role-1');
    await roles.createRole({ name: 'operator', displayName: 'Operator', permissions: [], acknowledgeWildcardGrant: false });
    await roles.updateRole('role-1', { displayName: 'Operator', permissions: [], acknowledgeWildcardGrant: false });
    await roles.deleteRole('role-1');
    await roles.getPlatformPermissionCatalog();
    await sessions.getSessions({ status: 'Active' });
    await sessions.getSession('session-1');
    await sessions.revokeSession('session-1');
    await sessions.revokeAllUserSessions('user-1');
    await settings.getAuthenticationSettings();
    await settings.getAuthenticationProviders();
    await settings.setDefaultProvider({ providerId: 'provider-1' });
    await settings.setLocalFallback({ enabled: true });
    await audit.getAuditEntries({ action: 'user.updated', userId: 'user-1' });

    expectCalls(client.get, [
      ['/api/admin/providers', { includeDisabled: true }],
      ['/api/admin/providers/provider-1'],
      ['/api/admin/roles', { search: 'operator' }],
      ['/api/admin/roles/role-1'],
      ['/api/admin/permissions/platform'],
      ['/api/admin/sessions', { status: 'Active' }],
      ['/api/admin/sessions/session-1'],
      ['/api/admin/authentication-settings'],
      ['/api/admin/authentication-settings/providers'],
      ['/api/admin/audit-entries', { action: 'user.updated', userId: 'user-1' }],
    ]);
    expectCalls(client.post, [
      ['/api/admin/providers', { name: 'github', authority: 'https://github.com', clientId: 'client-1' }],
      ['/api/admin/providers/provider-1/enable'],
      ['/api/admin/providers/provider-1/disable'],
      ['/api/admin/roles', { name: 'operator', displayName: 'Operator', permissions: [], acknowledgeWildcardGrant: false }],
    ]);
    expectCalls(client.patch, [['/api/admin/providers/provider-1', { displayName: 'GitHub' }]]);
    expectCalls(client.put, [
      ['/api/admin/roles/role-1', { displayName: 'Operator', permissions: [], acknowledgeWildcardGrant: false }],
      ['/api/admin/authentication-settings/default-provider', { providerId: 'provider-1' }],
      ['/api/admin/authentication-settings/local-fallback', { enabled: true }],
    ]);
    expectCalls(client.delete, [
      ['/api/admin/providers/provider-1'],
      ['/api/admin/roles/role-1'],
      ['/api/admin/sessions/session-1'],
      ['/api/admin/users/user-1/sessions'],
    ]);
  });
});
