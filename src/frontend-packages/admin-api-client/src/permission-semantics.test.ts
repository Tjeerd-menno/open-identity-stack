import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import {
  hasAnyPermission,
  hasEveryPermission,
  hasPermission,
} from './permission-semantics';

type PermissionSemanticsCase = {
  name: string;
  granted: string;
  required: string;
  matches: boolean;
};

const permissionSemanticsCases = JSON.parse(
  readFileSync(new URL('../../../../tests/PermissionSemantics/permission-semantics-cases.json', import.meta.url), 'utf8')
) as PermissionSemanticsCase[];

describe('permission semantics', () => {
  it('checks exact and wildcard permissions', () => {
    expect(hasPermission(['users:read'], 'users:read')).toBe(true);
    expect(hasPermission(['Users:Read'], 'users:read')).toBe(true);
    expect(hasPermission(['*'], 'users:read')).toBe(true);
    expect(hasPermission(['users:*'], 'users:update')).toBe(true);
    expect(hasPermission(['application:resource:*'], 'application:resource:read')).toBe(true);
    expect(hasPermission(['users:*'], 'users:settings:update')).toBe(false);
    expect(hasPermission(['application:resource:*'], 'application:other:read')).toBe(false);
  });

  it('checks any and every required permission', () => {
    expect(hasAnyPermission(['users:read'], ['roles:read', 'users:read'])).toBe(true);
    expect(hasAnyPermission(['users:read'], ['roles:read', 'groups:read'])).toBe(false);
    expect(hasEveryPermission(['users:*'], ['users:read', 'users:update'])).toBe(true);
    expect(hasEveryPermission(['users:read'], ['users:read', 'users:update'])).toBe(false);
  });

  describe.each(permissionSemanticsCases)('shared permission semantics: $name', (testCase) => {
    it('matches the shared result', () => {
      expect(hasPermission([testCase.granted], testCase.required)).toBe(testCase.matches);
    });
  });
});

