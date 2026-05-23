import { describe, expect, it } from 'vitest';
import { API_ENDPOINTS, APP_CONFIG, PERMISSIONS, ROUTES } from './constants';

describe('constants', () => {
  it('builds API endpoint URLs with interpolated ids', () => {
    expect(API_ENDPOINTS.USER('user-1')).toBe('/api/admin/users/user-1');
    expect(API_ENDPOINTS.USER_ROLE('user-1', 'role-2')).toBe('/api/admin/users/user-1/roles/role-2');
    expect(API_ENDPOINTS.GROUP_MEMBER('group-1', 'user-2')).toBe('/api/admin/groups/group-1/members/user-2');
    expect(API_ENDPOINTS.SERVICE_ACCOUNT_ROTATE_SECRET('svc-1')).toBe(
      '/api/admin/service-accounts/svc-1/rotate-secret'
    );
    expect(API_ENDPOINTS.SESSION_REVOKE_ALL_USER('user-1')).toBe('/api/admin/sessions/users/user-1/revoke-all');
  });

  it('builds route paths with interpolated ids', () => {
    expect(ROUTES.HOME).toBe('/');
    expect(ROUTES.LOGIN).toBe('/login');
    expect(ROUTES.USER_EDIT('user-1')).toBe('/users/user-1/edit');
    expect(ROUTES.ROLE_DETAIL('role-2')).toBe('/roles/role-2');
    expect(ROUTES.GROUP_CREATE).toBe('/groups/new');
    expect(ROUTES.PROVIDER_EDIT('provider-1')).toBe('/providers/provider-1/edit');
  });

  it('exposes permission constants as strings', () => {
    expect(PERMISSIONS.USERS_READ).toBe('users:read');
    expect(PERMISSIONS.ROLES_CREATE).toBe('roles:create');
    expect(PERMISSIONS.GROUPS_MANAGE_MEMBERS).toBe('groups:manage-members');
    expect(PERMISSIONS.SESSIONS_REVOKE).toBe('sessions:revoke');
  });

  it('defines the default page size', () => {
    expect(APP_CONFIG.DEFAULT_PAGE_SIZE).toBe(20);
  });
});
