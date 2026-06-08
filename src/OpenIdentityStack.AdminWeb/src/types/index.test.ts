import { describe, expect, it } from 'vitest';
import { hasPermission, isApiError } from './index';

describe('type helpers', () => {
  it('checks permissions on authenticated users', () => {
    expect(
      hasPermission(
        {
          sub: 'user-1',
          email: 'alice@example.com',
          name: 'Alice',
          permissions: ['users:*'],
        },
        'users:read'
      )
    ).toBe(true);
    expect(
      hasPermission(
        {
          sub: 'user-1',
          email: 'alice@example.com',
          name: 'Alice',
          permissions: ['users:read'],
        },
        'users:delete'
      )
    ).toBe(false);
    expect(hasPermission(null, 'users:read')).toBe(false);
  });

  it('recognizes API error-shaped values', () => {
    expect(isApiError({ status: 404, title: 'Not found' })).toBe(true);
    expect(isApiError({ status: 404 })).toBe(false);
    expect(isApiError(null)).toBe(false);
  });
});
