import { afterEach, describe, expect, it, vi } from 'vitest';
import { createAdminApiClient } from './index';
import { createCurrentUserContract } from './current-user';

const originalFetch = globalThis.fetch;

afterEach(() => {
  globalThis.fetch = originalFetch;
});

describe('current user contract', () => {
  it('gets the current user from /api/me', async () => {
    const response = {
      subject: 'user-123',
      userName: 'ada',
      displayName: 'Ada Lovelace',
      email: 'ada@example.com',
      permissions: ['users:read'],
    };
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(response)));
    globalThis.fetch = fetchMock;

    const client = createAdminApiClient({ baseUrl: 'https://admin.example' });
    const currentUser = createCurrentUserContract(client);

    await expect(currentUser.getCurrentUser()).resolves.toEqual(response);
    expect(fetchMock).toHaveBeenCalledWith('https://admin.example/api/me', expect.any(Object));
  });
});
