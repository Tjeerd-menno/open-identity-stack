import { afterEach, describe, expect, it, vi } from 'vitest';
import { createAdminApiClient } from './index';
import { createGroupsContract } from './groups';

const originalFetch = globalThis.fetch;

afterEach(() => {
  globalThis.fetch = originalFetch;
});

describe('groups contract', () => {
  it('resolves void when adding a mapping, matching the endpoint’s empty 201', async () => {
    // The endpoint answers 201 with a Location header and no body, so the client resolves
    // undefined. Declaring a GroupMapping return type here promised callers a value that never
    // arrives, and would fail only at runtime.
    globalThis.fetch = vi.fn().mockResolvedValueOnce(
      new Response(null, {
        status: 201,
        headers: { Location: '/api/admin/groups/group-1/mappings' },
      })
    );
    const groups = createGroupsContract(createAdminApiClient({ baseUrl: 'https://admin.example' }));

    await expect(
      groups.addGroupMapping('group-1', { type: 'Role', value: 'admin' })
    ).resolves.toBeUndefined();
  });

  it('returns the mapping list with a null createdAt', async () => {
    // createdAt is always null: a group mapping is a value object with no creation timestamp.
    globalThis.fetch = vi.fn().mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          items: [{ id: 'mapping-1', type: 'Claim', value: 'department:Engineering', createdAt: null }],
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );
    const groups = createGroupsContract(createAdminApiClient({ baseUrl: 'https://admin.example' }));

    const response = await groups.getGroupMappings('group-1');

    expect(response.items).toHaveLength(1);
    expect(response.items[0].createdAt).toBeNull();
  });
});
