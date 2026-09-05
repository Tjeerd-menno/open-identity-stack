import { vi } from 'vitest';

/**
 * A vitest mock of the `@/lib/api` `api` object. Tests import this and configure
 * return values; mock `@/lib/api` to serve it via:
 *
 *   vi.mock('@/lib/api', async () => {
 *     const { mockApi } = await import('@/test/mock-api');
 *     return { api: mockApi, getApiErrorMessage: (e: unknown) => ..., isApiError: () => false };
 *   });
 */
export const mockApi = {
  users: {
    getUsers: vi.fn(),
    getUser: vi.fn(),
    createUser: vi.fn(),
    updateUser: vi.fn(),
    disableUser: vi.fn(),
    enableUser: vi.fn(),
    deleteUser: vi.fn(),
    resetUserPassword: vi.fn(),
    getUserRoles: vi.fn(),
    assignUserRole: vi.fn(),
    unassignUserRole: vi.fn(),
    getUserGroups: vi.fn(),
    getUserUpstreamIdentities: vi.fn(),
    getIdentityMigrationInventory: vi.fn(),
    unlinkUserUpstreamIdentity: vi.fn(),
  },
  applications: {
    listProtectedResources: vi.fn().mockResolvedValue([]),
    createProtectedResource: vi.fn(),
    configureProtectedResource: vi.fn(),
    listClientResourceGrants: vi.fn().mockResolvedValue([]),
    configureClientResourceGrant: vi.fn(),
    getApplications: vi.fn(),
    getApplication: vi.fn(),
    getApplicationProfilePolicies: vi.fn(),
    createApplication: vi.fn(),
    updateApplicationMetadata: vi.fn(),
    configureApplicationOAuth: vi.fn(),
    disableApplication: vi.fn(),
    enableApplication: vi.fn(),
    deleteApplication: vi.fn(),
    listApplicationCredentials: vi.fn(),
    addApplicationSecret: vi.fn(),
    revokeApplicationCredential: vi.fn(),
  },
  roles: {
    getRoles: vi.fn(),
    getRole: vi.fn(),
    createRole: vi.fn(),
    updateRole: vi.fn(),
    deleteRole: vi.fn(),
    getPlatformPermissionCatalog: vi.fn(),
  },
  groups: {
    getGroups: vi.fn(),
    getGroup: vi.fn(),
    createGroup: vi.fn(),
    updateGroup: vi.fn(),
    deleteGroup: vi.fn(),
    getGroupMembers: vi.fn(),
    addMemberToGroup: vi.fn(),
    removeMemberFromGroup: vi.fn(),
    getGroupMappings: vi.fn(),
    addGroupMapping: vi.fn(),
    removeGroupMapping: vi.fn(),
  },
  sessions: {
    getSessions: vi.fn(),
    getSession: vi.fn(),
    revokeSession: vi.fn(),
  },
  providers: {
    getProviders: vi.fn(),
    getProvider: vi.fn(),
    createProvider: vi.fn(),
    updateProvider: vi.fn(),
    enableProvider: vi.fn(),
    disableProvider: vi.fn(),
    deleteProvider: vi.fn(),
    setEmailVerificationTrust: vi.fn(),
  },
  settings: {
    getAuthenticationSettings: vi.fn(),
    getAuthenticationProviders: vi.fn(),
    setDefaultProvider: vi.fn(),
    setLocalFallback: vi.fn(),
  },
  audit: {
    getAuditEntries: vi.fn(),
  },
  applicationPermissions: {
    getRegisteredApplications: vi.fn(),
    getRegisteredApplication: vi.fn(),
    registerPermissionManifest: vi.fn(),
    importPermissionManifestFromEndpoint: vi.fn(),
    changeApplicationLifecycle: vi.fn(),
  },
  currentUser: {
    getCurrentUser: vi.fn(),
  },
};

export function page<T>(items: T[], overrides: Record<string, unknown> = {}) {
  return { items, totalCount: items.length, page: 1, pageSize: 25, totalPages: 1, ...overrides };
}

export function resetApiMocks() {
  for (const domain of Object.values(mockApi)) {
    for (const fn of Object.values(domain)) {
      (fn as ReturnType<typeof vi.fn>).mockReset();
    }
  }
}
