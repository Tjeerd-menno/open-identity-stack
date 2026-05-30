import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  createClient,
  deleteClient,
  getClient,
  getClients,
  updateClient,
} from './clients/api/clients-api';
import {
  addGroupMapping,
  addMemberToGroup,
  createGroup,
  deleteGroup,
  getGroup,
  getGroupMappings,
  getGroupMembers,
  getGroups,
  removeGroupMapping,
  removeMemberFromGroup,
  updateGroup,
} from './groups/api/groups-api';
import { providersApi } from './providers/api/providers-api';
import {
  createRole,
  deleteRole,
  getAvailablePermissions,
  getRole,
  getRoles,
  updateRole,
} from './roles/api/roles-api';
import {
  getSession,
  getSessions,
  revokeAllUserSessions,
  revokeSession,
} from './sessions/api/sessions-api';
import {
  addCertificate,
  createServiceAccount,
  deleteServiceAccount,
  disableServiceAccount,
  enableServiceAccount,
  getServiceAccount,
  getServiceAccountCertificates,
  getServiceAccounts,
  rotateSecret,
  updateServiceAccount,
} from './service-accounts/api/service-accounts-api';
import {
  addDelegatedMaintainer,
  addApplicationPermission,
  changeApplicationLifecycle,
  getAssignablePermissionCatalog,
  getPermissionDependencies,
  getRegisteredApplication,
  getRegisteredApplications,
  registerPermissionManifest,
  removeDelegatedMaintainer,
  transferApplicationOwnership,
  updateRegisteredApplication,
} from './application-permissions/api/application-permissions-api';
import { settingsApi } from './settings/api/settings-api';
import {
  assignRole,
  createUser,
  deleteUser,
  disableUser,
  enableUser,
  getUpstreamIdentities,
  getUser,
  getUserGroups,
  getUserRoles,
  getUsers,
  linkUpstreamIdentity,
  resetPassword,
  unassignRole,
  unlinkUpstreamIdentity,
  updateUser,
} from './users/api/users-api';
import { ClientType, MappingType, ProviderStatus } from '@/types';

const { apiClient } = vi.hoisted(() => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock('@/lib/api/client', () => ({
  apiClient,
  default: apiClient,
}));

describe('admin API clients', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('clients', () => {
    it('lists clients with pagination parameters', async () => {
      const response = { items: [], totalCount: 0, page: 2, pageSize: 25 };
      apiClient.get.mockResolvedValueOnce(response);

      await expect(getClients({ page: 2, pageSize: 25, search: 'web' })).resolves.toBe(response);

      expect(apiClient.get).toHaveBeenCalledWith('/api/admin/clients', {
        page: 2,
        pageSize: 25,
        search: 'web',
      });
    });

    it('gets a client by id', async () => {
      apiClient.get.mockResolvedValueOnce({ id: 'client-1' });

      await getClient('client-1');

      expect(apiClient.get).toHaveBeenCalledWith('/api/admin/clients/client-1');
    });

    it('creates a client with redirect and grant settings', async () => {
      const request = {
        clientId: 'admin-web',
        displayName: 'Admin Web',
        clientType: ClientType.Public,
        description: null,
        redirectUris: ['https://app.example/auth/callback'],
        postLogoutRedirectUris: ['https://app.example/'],
        allowedScopes: ['openid', 'api'],
        allowedGrantTypes: ['authorization_code'],
        requirePkce: true,
        requireConsent: false,
      };
      apiClient.post.mockResolvedValueOnce({ id: 'client-1' });

      await createClient(request);

      expect(apiClient.post).toHaveBeenCalledWith('/api/admin/clients', request);
    });

    it('updates a client using patch', async () => {
      const request = { displayName: 'Renamed client', description: 'Updated' };
      apiClient.patch.mockResolvedValueOnce({ id: 'client-1' });

      await updateClient('client-1', request);

      expect(apiClient.patch).toHaveBeenCalledWith('/api/admin/clients/client-1', request);
    });

    it('deletes a client', async () => {
      apiClient.delete.mockResolvedValueOnce(undefined);

      await deleteClient('client-1');

      expect(apiClient.delete).toHaveBeenCalledWith('/api/admin/clients/client-1');
    });
  });

  describe('users', () => {
    it('lists users with pagination parameters', async () => {
      apiClient.get.mockResolvedValueOnce({ items: [] });

      await getUsers({ page: 3, pageSize: 50, search: 'alice' });

      expect(apiClient.get).toHaveBeenCalledWith('/api/admin/users', {
        page: 3,
        pageSize: 50,
        search: 'alice',
      });
    });

    it('gets, creates, and updates users through the expected endpoints', async () => {
      apiClient.get.mockResolvedValueOnce({ id: 'user-1' });
      apiClient.post.mockResolvedValueOnce({ id: 'user-2' });
      apiClient.put.mockResolvedValueOnce({ id: 'user-1' });

      await getUser('user-1');
      await createUser({ email: 'new@example.com', displayName: 'New User', password: 'Password1' });
      await updateUser('user-1', { displayName: 'Updated User' });

      expect(apiClient.get).toHaveBeenCalledWith('/api/admin/users/user-1');
      expect(apiClient.post).toHaveBeenCalledWith('/api/admin/users', {
        email: 'new@example.com',
        displayName: 'New User',
        password: 'Password1',
      });
      expect(apiClient.put).toHaveBeenCalledWith('/api/admin/users/user-1', {
        displayName: 'Updated User',
      });
    });

    it('sends user status and password mutations to action endpoints', async () => {
      apiClient.post.mockResolvedValue({ userId: 'user-1' });

      await disableUser('user-1', { reason: 'Compromised' });
      await enableUser('user-1');
      await resetPassword('user-1', { newPassword: 'Password1' });

      expect(apiClient.post).toHaveBeenNthCalledWith(
        1,
        '/api/admin/users/user-1/disable',
        { reason: 'Compromised' }
      );
      expect(apiClient.post).toHaveBeenNthCalledWith(2, '/api/admin/users/user-1/enable');
      expect(apiClient.post).toHaveBeenNthCalledWith(
        3,
        '/api/admin/users/user-1/reset-password',
        { newPassword: 'Password1' }
      );
    });

    it('maps role and identity collection responses to arrays', async () => {
      const roles = [{ id: 'role-1', name: 'admin' }];
      const identities = [{ providerId: 'google', subjectId: 'sub-1' }];
      apiClient.get
        .mockResolvedValueOnce({ userId: 'user-1', roles })
        .mockResolvedValueOnce([{ id: 'group-1' }])
        .mockResolvedValueOnce({ items: identities });

      await expect(getUserRoles('user-1')).resolves.toBe(roles);
      await expect(getUserGroups('user-1')).resolves.toEqual([{ id: 'group-1' }]);
      await expect(getUpstreamIdentities('user-1')).resolves.toBe(identities);
    });

    it('assigns and removes roles and upstream identities', async () => {
      apiClient.post.mockResolvedValue(undefined);
      apiClient.delete.mockResolvedValue(undefined);

      await assignRole('user-1', 'role-1');
      await unassignRole('user-1', 'role-1');
      await linkUpstreamIdentity('user-1', {
        providerId: 'google',
        subjectId: 'sub-1',
        email: 'alice@example.com',
      });
      await unlinkUpstreamIdentity('user-1', 'google');
      await deleteUser('user-1');

      expect(apiClient.post).toHaveBeenNthCalledWith(1, '/api/admin/users/user-1/roles/role-1');
      expect(apiClient.delete).toHaveBeenNthCalledWith(1, '/api/admin/users/user-1/roles/role-1');
      expect(apiClient.post).toHaveBeenNthCalledWith(
        2,
        '/api/admin/users/user-1/upstream-identities',
        { providerId: 'google', subjectId: 'sub-1', email: 'alice@example.com' }
      );
      expect(apiClient.delete).toHaveBeenNthCalledWith(
        2,
        '/api/admin/users/user-1/upstream-identities/google'
      );
      expect(apiClient.delete).toHaveBeenNthCalledWith(3, '/api/admin/users/user-1');
    });
  });

  describe('groups', () => {
    it('lists groups using query string parameters', async () => {
      apiClient.get.mockResolvedValueOnce({ items: [] });

      await getGroups(4, 10, 'platform');

      expect(apiClient.get).toHaveBeenCalledWith('/api/admin/groups?page=4&pageSize=10&search=platform');
    });

    it('omits empty group search parameters', async () => {
      apiClient.get.mockResolvedValueOnce({ items: [] });

      await getGroups();

      expect(apiClient.get).toHaveBeenCalledWith('/api/admin/groups?page=1&pageSize=20');
    });

    it('covers group CRUD endpoints', async () => {
      apiClient.get.mockResolvedValueOnce({ id: 'group-1' });
      apiClient.post.mockResolvedValueOnce({ id: 'group-2' });
      apiClient.patch.mockResolvedValueOnce({ id: 'group-1' });
      apiClient.delete.mockResolvedValueOnce(undefined);

      await getGroup('group-1');
      await createGroup({ name: 'Developers', description: 'Builds software' });
      await updateGroup('group-1', { name: 'Platform' });
      await deleteGroup('group-1');

      expect(apiClient.get).toHaveBeenCalledWith('/api/admin/groups/group-1');
      expect(apiClient.post).toHaveBeenCalledWith('/api/admin/groups', {
        name: 'Developers',
        description: 'Builds software',
      });
      expect(apiClient.patch).toHaveBeenCalledWith('/api/admin/groups/group-1', {
        name: 'Platform',
      });
      expect(apiClient.delete).toHaveBeenCalledWith('/api/admin/groups/group-1');
    });

    it('manages group members and mappings', async () => {
      apiClient.get
        .mockResolvedValueOnce({ items: [] })
        .mockResolvedValueOnce({ items: [{ id: 'mapping-1' }] });
      apiClient.post.mockResolvedValue({ id: 'mapping-2' });
      apiClient.delete.mockResolvedValue(undefined);

      await getGroupMembers('group-1', 2, 5);
      await addMemberToGroup('group-1', 'user-1');
      await removeMemberFromGroup('group-1', 'user-1');
      await getGroupMappings('group-1');
      await addGroupMapping('group-1', { type: MappingType.Role, value: 'admin' });
      await removeGroupMapping('group-1', 'mapping-1');

      expect(apiClient.get).toHaveBeenNthCalledWith(
        1,
        '/api/admin/groups/group-1/members?page=2&pageSize=5'
      );
      expect(apiClient.post).toHaveBeenNthCalledWith(1, '/api/admin/groups/group-1/members/user-1');
      expect(apiClient.delete).toHaveBeenNthCalledWith(1, '/api/admin/groups/group-1/members/user-1');
      expect(apiClient.get).toHaveBeenNthCalledWith(2, '/api/admin/groups/group-1/mappings');
      expect(apiClient.post).toHaveBeenNthCalledWith(
        2,
        '/api/admin/groups/group-1/mappings',
        { type: MappingType.Role, value: 'admin' }
      );
      expect(apiClient.delete).toHaveBeenNthCalledWith(
        2,
        '/api/admin/groups/group-1/mappings/mapping-1'
      );
    });
  });

  describe('roles', () => {
    it('lists roles with defaults and optional search', async () => {
      apiClient.get.mockResolvedValueOnce({ items: [] }).mockResolvedValueOnce({ items: [] });

      await getRoles();
      await getRoles({ page: 2, pageSize: 5, search: 'admin' });

      expect(apiClient.get).toHaveBeenNthCalledWith(1, '/api/admin/roles', {
        page: 1,
        pageSize: 20,
      });
      expect(apiClient.get).toHaveBeenNthCalledWith(2, '/api/admin/roles', {
        page: 2,
        pageSize: 5,
        search: 'admin',
      });
    });

    it('covers role CRUD endpoints', async () => {
      apiClient.get.mockResolvedValueOnce({ id: 'role-1' });
      apiClient.post.mockResolvedValueOnce({ id: 'role-2' });
      apiClient.put.mockResolvedValueOnce({ id: 'role-1' });
      apiClient.delete.mockResolvedValueOnce(undefined);

      await getRole('role-1');
      await createRole({ name: 'auditor', displayName: 'Auditor', permissions: ['users:read'] });
      await updateRole('role-1', { displayName: 'Auditor' });
      await deleteRole('role-1');

      expect(apiClient.get).toHaveBeenCalledWith('/api/admin/roles/role-1');
      expect(apiClient.post).toHaveBeenCalledWith('/api/admin/roles', {
        name: 'auditor',
        displayName: 'Auditor',
        permissions: ['users:read'],
      });
      expect(apiClient.put).toHaveBeenCalledWith('/api/admin/roles/role-1', {
        displayName: 'Auditor',
      });
      expect(apiClient.delete).toHaveBeenCalledWith('/api/admin/roles/role-1');
    });

    it('combines built-in and registered permissions without duplicates', async () => {
      apiClient.get.mockResolvedValueOnce({
        items: [
          { fullPermissionKey: 'users:read' },
          { fullPermissionKey: 'inventory:read' },
        ],
      });

      await expect(getAvailablePermissions()).resolves.toEqual(
        expect.arrayContaining(['users:read', 'roles:create', 'inventory:read'])
      );
      expect(apiClient.get).toHaveBeenCalledWith('/api/admin/application-permissions/catalog', {
        page: 1,
        pageSize: 100,
      });
    });

    it('falls back to built-in permissions when the catalog fails', async () => {
      apiClient.get.mockRejectedValueOnce(new Error('catalog unavailable'));

      await expect(getAvailablePermissions()).resolves.toEqual(
        expect.arrayContaining(['users:read', 'sessions:revoke'])
      );
    });
  });

  describe('sessions', () => {
    it('lists and loads sessions', async () => {
      apiClient.get.mockResolvedValueOnce({ items: [] }).mockResolvedValueOnce({ id: 'session-1' });

      await getSessions({ page: 1, status: 'Active' });
      await getSession('session-1');

      expect(apiClient.get).toHaveBeenNthCalledWith(1, '/api/admin/sessions', {
        page: 1,
        status: 'Active',
      });
      expect(apiClient.get).toHaveBeenNthCalledWith(2, '/api/admin/sessions/session-1');
    });

    it('revokes one session or all sessions for a user', async () => {
      apiClient.delete.mockResolvedValueOnce({ sessionId: 'session-1' });
      apiClient.post.mockResolvedValueOnce({ revokedCount: 2 });

      await revokeSession('session-1');
      await revokeAllUserSessions('user-1');

      expect(apiClient.delete).toHaveBeenCalledWith('/api/admin/sessions/session-1');
      expect(apiClient.post).toHaveBeenCalledWith('/api/admin/users/user-1/sessions/revoke-all');
    });
  });

  describe('service accounts', () => {
    it('covers service account read and write endpoints', async () => {
      apiClient.get.mockResolvedValueOnce({ items: [] }).mockResolvedValueOnce({ id: 'sa-1' });
      apiClient.post.mockResolvedValueOnce({ id: 'sa-2' });
      apiClient.patch.mockResolvedValueOnce({ id: 'sa-1' });
      apiClient.delete.mockResolvedValueOnce(undefined);

      await getServiceAccounts({ search: 'worker' });
      await getServiceAccount('sa-1');
      await createServiceAccount({
        clientId: 'worker',
        displayName: 'Worker',
        allowedScopes: ['api'],
        allowedGrantTypes: ['client_credentials'],
      });
      await updateServiceAccount('sa-1', { displayName: 'Renamed Worker' });
      await deleteServiceAccount('sa-1');

      expect(apiClient.get).toHaveBeenNthCalledWith(1, '/api/admin/service-accounts', {
        search: 'worker',
      });
      expect(apiClient.get).toHaveBeenNthCalledWith(2, '/api/admin/service-accounts/sa-1');
      expect(apiClient.post).toHaveBeenCalledWith('/api/admin/service-accounts', {
        clientId: 'worker',
        displayName: 'Worker',
        allowedScopes: ['api'],
        allowedGrantTypes: ['client_credentials'],
      });
      expect(apiClient.patch).toHaveBeenCalledWith('/api/admin/service-accounts/sa-1', {
        displayName: 'Renamed Worker',
      });
      expect(apiClient.delete).toHaveBeenCalledWith('/api/admin/service-accounts/sa-1');
    });

    it('uses service account action endpoints', async () => {
      apiClient.post.mockResolvedValue(undefined);
      apiClient.get.mockResolvedValueOnce([]);

      await enableServiceAccount('sa-1');
      await disableServiceAccount('sa-1');
      await rotateSecret('sa-1', { revokeExisting: true, description: 'rotation' });
      await addCertificate('sa-1', {
        thumbprint: 'thumb',
        subject: 'CN=worker',
        expiresAt: '2027-01-01',
      });
      await getServiceAccountCertificates('sa-1');

      expect(apiClient.post).toHaveBeenNthCalledWith(1, '/api/admin/service-accounts/sa-1/enable');
      expect(apiClient.post).toHaveBeenNthCalledWith(2, '/api/admin/service-accounts/sa-1/disable');
      expect(apiClient.post).toHaveBeenNthCalledWith(
        3,
        '/api/admin/service-accounts/sa-1/rotate-secret',
        { revokeExisting: true, description: 'rotation' }
      );
      expect(apiClient.post).toHaveBeenNthCalledWith(
        4,
        '/api/admin/service-accounts/sa-1/certificates',
        { thumbprint: 'thumb', subject: 'CN=worker', expiresAt: '2027-01-01' }
      );
      expect(apiClient.get).toHaveBeenCalledWith('/api/admin/service-accounts/sa-1/certificates');
    });
  });

  describe('providers and settings', () => {
    it('covers provider endpoints', async () => {
      apiClient.get.mockResolvedValueOnce([]).mockResolvedValueOnce({ id: 'provider-1' });
      apiClient.post.mockResolvedValue({ id: 'provider-1' });
      apiClient.patch.mockResolvedValueOnce({ id: 'provider-1' });
      apiClient.delete.mockResolvedValueOnce(undefined);

      await providersApi.getProviders(true);
      await providersApi.getProvider('provider-1');
      await providersApi.createProvider({
        name: 'google',
        displayName: 'Google',
        authority: 'https://accounts.google.com',
        clientId: 'client-id',
      });
      await providersApi.updateProvider('provider-1', { status: ProviderStatus.Disabled });
      await providersApi.enableProvider('provider-1');
      await providersApi.disableProvider('provider-1');
      await providersApi.deleteProvider('provider-1');

      expect(apiClient.get).toHaveBeenNthCalledWith(1, '/api/admin/providers', {
        includeDisabled: true,
      });
      expect(apiClient.get).toHaveBeenNthCalledWith(2, '/api/admin/providers/provider-1');
      expect(apiClient.post).toHaveBeenNthCalledWith(1, '/api/admin/providers', {
        name: 'google',
        displayName: 'Google',
        authority: 'https://accounts.google.com',
        clientId: 'client-id',
      });
      expect(apiClient.patch).toHaveBeenCalledWith('/api/admin/providers/provider-1', {
        status: ProviderStatus.Disabled,
      });
      expect(apiClient.post).toHaveBeenNthCalledWith(2, '/api/admin/providers/provider-1/enable');
      expect(apiClient.post).toHaveBeenNthCalledWith(3, '/api/admin/providers/provider-1/disable');
      expect(apiClient.delete).toHaveBeenCalledWith('/api/admin/providers/provider-1');
    });

    it('covers authentication settings endpoints', async () => {
      apiClient.get.mockResolvedValueOnce({}).mockResolvedValueOnce([]);
      apiClient.put.mockResolvedValue({});

      await settingsApi.getAuthenticationSettings();
      await settingsApi.getAuthenticationProviders();
      await settingsApi.setDefaultProvider({ providerId: 'provider-1' });
      await settingsApi.setLocalFallback({ enabled: true });

      expect(apiClient.get).toHaveBeenNthCalledWith(1, '/api/admin/authentication-settings');
      expect(apiClient.get).toHaveBeenNthCalledWith(2, '/api/admin/authentication-settings/providers');
      expect(apiClient.put).toHaveBeenNthCalledWith(
        1,
        '/api/admin/authentication-settings/default-provider',
        { providerId: 'provider-1' }
      );
      expect(apiClient.put).toHaveBeenNthCalledWith(
        2,
        '/api/admin/authentication-settings/local-fallback',
        { enabled: true }
      );
    });
  });

  describe('application permissions', () => {
    it('maps application permission registry endpoints', async () => {
      apiClient.get.mockResolvedValueOnce({ items: [] }).mockResolvedValueOnce({ id: 'application-1' });
      apiClient.post.mockResolvedValue({ id: 'application-1' });
      apiClient.patch.mockResolvedValueOnce({ id: 'application-1' });

      await getRegisteredApplications({ page: 2, search: 'inventory' });
      await getRegisteredApplication('application-1');
      await registerPermissionManifest({
        manifest: {
          schemaVersion: '1.0.0',
          application: {
            id: 'inventory-api',
            displayName: 'Inventory API',
            version: '1.0.0',
          },
          permissions: [{ key: 'inventory:read', displayName: 'Read inventory', description: 'Read inventory' }],
        },
        ownerId: 'owner-1',
        ownerType: 'user',
      });
      await updateRegisteredApplication('application-1', {
        displayName: 'Inventory',
        concurrencyToken: 3,
      });
      await addApplicationPermission('application-1', {
        permissionKey: 'write',
        displayName: 'Write inventory',
      });

      expect(apiClient.get).toHaveBeenNthCalledWith(
        1,
        '/api/admin/application-permissions/applications',
        { page: 2, search: 'inventory' }
      );
      expect(apiClient.get).toHaveBeenNthCalledWith(
        2,
        '/api/admin/application-permissions/applications/application-1'
      );
      expect(apiClient.post).toHaveBeenNthCalledWith(
        1,
        '/api/admin/application-permissions/applications',
        expect.objectContaining({ manifest: expect.objectContaining({ application: expect.objectContaining({ id: 'inventory-api' }) }) })
      );
      expect(apiClient.patch).toHaveBeenCalledWith(
        '/api/admin/application-permissions/applications/application-1',
        { displayName: 'Inventory', concurrencyToken: 3 }
      );
      expect(apiClient.post).toHaveBeenNthCalledWith(
        2,
        '/api/admin/application-permissions/applications/application-1/permissions',
        { permissionKey: 'write', displayName: 'Write inventory' }
      );
    });

    it('maps application ownership and maintainer actions', async () => {
      apiClient.post.mockResolvedValue({ id: 'application-1' });
      apiClient.delete.mockResolvedValue({ id: 'application-1' });
      apiClient.get.mockResolvedValue({ items: [] });

      await changeApplicationLifecycle('application-1', {
        status: 'Disabled',
        acknowledgeDependencies: true,
        concurrencyToken: 4,
      });
      await getPermissionDependencies('permission-1');
      await transferApplicationOwnership('application-1', {
        ownerId: 'owner-2',
        ownerType: 'Group',
      });
      await addDelegatedMaintainer('application-1', {
        principalId: 'maintainer-1',
        principalType: 'User',
      });
      await removeDelegatedMaintainer('application-1', 'maintainer@example.com', 9);
      await getAssignablePermissionCatalog({ pageSize: 100 });

      expect(apiClient.post).toHaveBeenNthCalledWith(
        1,
        '/api/admin/application-permissions/applications/application-1/lifecycle',
        { status: 'Disabled', acknowledgeDependencies: true, concurrencyToken: 4 }
      );
      expect(apiClient.get).toHaveBeenNthCalledWith(
        1,
        '/api/admin/application-permissions/permissions/permission-1/dependencies'
      );
      expect(apiClient.post).toHaveBeenNthCalledWith(
        2,
        '/api/admin/application-permissions/applications/application-1/ownership',
        { ownerId: 'owner-2', ownerType: 'Group' }
      );
      expect(apiClient.post).toHaveBeenNthCalledWith(
        3,
        '/api/admin/application-permissions/applications/application-1/maintainers',
        { principalId: 'maintainer-1', principalType: 'User' }
      );
      expect(apiClient.delete).toHaveBeenCalledWith(
        '/api/admin/application-permissions/applications/application-1/maintainers/maintainer%40example.com?concurrencyToken=9'
      );
      expect(apiClient.get).toHaveBeenNthCalledWith(
        2,
        '/api/admin/application-permissions/catalog',
        { pageSize: 100 }
      );
    });

    it('omits maintainer concurrency token when it is not provided', async () => {
      apiClient.delete.mockResolvedValueOnce({ id: 'application-1' });

      await removeDelegatedMaintainer('application-1', 'owner@example.com');

      expect(apiClient.delete).toHaveBeenCalledWith(
        '/api/admin/application-permissions/applications/application-1/maintainers/owner%40example.com'
      );
    });
  });
});
