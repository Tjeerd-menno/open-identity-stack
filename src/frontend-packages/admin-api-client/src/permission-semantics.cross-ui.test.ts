import { describe, expect, it } from 'vitest';
import {
  hasEveryPermission,
  hasPermission,
} from './permission-semantics';

describe('cross-UI permission semantics regressions', () => {
  it.each([
    {
      name: 'global wildcard grant',
      granted: ['*'],
      required: 'application-permissions:read',
      expected: true,
    },
    {
      name: 'resource wildcard grant',
      granted: ['users:*'],
      required: 'users:update',
      expected: true,
    },
    {
      name: 'nested resource wildcard grant',
      granted: ['application:resource:*'],
      required: 'application:resource:read',
      expected: true,
    },
    {
      name: 'resource wildcard does not match deeper requirements',
      granted: ['users:*'],
      required: 'users:settings:update',
      expected: false,
    },
  ])('evaluates $name consistently', ({ granted, required, expected }) => {
    expect(hasPermission(granted, required)).toBe(expected);
  });

  it('requires every permission through wildcard-aware matching', () => {
    expect(hasEveryPermission(['application-permissions:*'], [
      'application-permissions:read',
      'application-permissions:write',
    ])).toBe(true);
  });
});
