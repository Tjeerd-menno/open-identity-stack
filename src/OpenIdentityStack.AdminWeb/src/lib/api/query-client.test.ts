import { describe, expect, it } from 'vitest';
import { queryClient, queryKeys } from './query-client';

describe('query client configuration', () => {
  it('does not retry auth or client errors and retries transient errors twice', () => {
    const retry = queryClient.getDefaultOptions().queries?.retry;
    expect(typeof retry).toBe('function');
    const shouldRetry = retry as (failureCount: number, error: unknown) => boolean;

    expect(shouldRetry(0, { status: 401 })).toBe(false);
    expect(shouldRetry(0, { status: 403 })).toBe(false);
    expect(shouldRetry(0, { status: 404 })).toBe(false);
    expect(shouldRetry(0, { status: 422 })).toBe(false);
    expect(shouldRetry(0, { status: 500 })).toBe(true);
    expect(shouldRetry(1, new Error('network'))).toBe(true);
    expect(shouldRetry(2, new Error('network'))).toBe(false);
  });

  it('defines stable query keys for all admin resources', () => {
    expect(queryKeys.users.list({ search: 'jane' })).toEqual(['users', 'list', { search: 'jane' }]);
    expect(queryKeys.users.detail('user-1')).toEqual(['users', 'detail', 'user-1']);
    expect(queryKeys.users.roles('user-1')).toEqual(['users', 'detail', 'user-1', 'roles']);
    expect(queryKeys.users.groups('user-1')).toEqual(['users', 'detail', 'user-1', 'groups']);
    expect(queryKeys.users.upstreamIdentities('user-1')).toEqual([
      'users',
      'detail',
      'user-1',
      'upstream-identities',
    ]);

    expect(queryKeys.roles.list()).toEqual(['roles', 'list', undefined]);
    expect(queryKeys.roles.detail('role-1')).toEqual(['roles', 'detail', 'role-1']);
    expect(queryKeys.groups.members('group-1')).toEqual(['groups', 'detail', 'group-1', 'members']);
    expect(queryKeys.groups.mappings('group-1')).toEqual(['groups', 'detail', 'group-1', 'mappings']);
    expect(queryKeys.serviceAccounts.detail('service-1')).toEqual([
      'service-accounts',
      'detail',
      'service-1',
    ]);
    expect(queryKeys.sessions.detail('session-1')).toEqual(['sessions', 'detail', 'session-1']);
    expect(queryKeys.providers.detail('provider-1')).toEqual(['providers', 'detail', 'provider-1']);
  });
});
