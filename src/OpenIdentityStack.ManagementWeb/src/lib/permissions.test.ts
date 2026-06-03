import { describe, expect, it } from 'vitest';
import { hasAnyPermission, hasEveryPermission, hasPermission } from './permissions';

describe('permission foundation', () => {
  it('supports global and resource wildcards', () => {
    expect(hasPermission(['*'], 'users:read')).toBe(true);
    expect(hasPermission(['applications:*'], 'applications:manage-credentials')).toBe(true);
    expect(hasPermission(['applications:*'], 'roles:read')).toBe(false);
  });

  it('matches permissions case-insensitively', () => {
    expect(hasPermission(['Users:Read'], 'users:read')).toBe(true);
  });

  it('evaluates any and every permission collections', () => {
    expect(hasAnyPermission(['users:read'], ['roles:read', 'users:read'])).toBe(true);
    expect(hasAnyPermission(['users:read'], ['roles:read', 'groups:read'])).toBe(false);
    expect(hasEveryPermission(['users:*'], ['users:read', 'users:update'])).toBe(true);
    expect(hasEveryPermission(['users:read'], ['users:read', 'users:update'])).toBe(false);
  });
});
