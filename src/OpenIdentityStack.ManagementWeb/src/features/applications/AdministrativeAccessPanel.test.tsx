import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderManagementWeb } from '@/test/render';
import { AdministrativeAccessPanel } from './AdministrativeAccessPanel';

const originalFetch = globalThis.fetch;
afterEach(() => { globalThis.fetch = originalFetch; });

describe('AdministrativeAccessPanel', () => {
  it('shows an unapproved client and submits separate reviewed ceilings', async () => {
    const fetchMock = vi.fn().mockImplementation(async (_url, options) => new Response(JSON.stringify(
      options?.method === 'PUT'
        ? { approved: true, delegatedPermissions: ['users:read'], applicationPermissions: [], revision: 1 }
        : { approved: false, delegatedPermissions: [], applicationPermissions: [], revision: null }
    ), { status: 200 }));
    globalThis.fetch = fetchMock;
    const user = userEvent.setup();
    renderManagementWeb(<AdministrativeAccessPanel applicationId="client-one" canWrite />);
    expect(await screen.findByText('Not approved')).toBeInTheDocument();
    await user.type(screen.getByLabelText('Delegated permission ceiling'), 'users:read');
    await user.click(screen.getByRole('button', { name: 'Save administrative access' }));
    await waitFor(() => expect(fetchMock.mock.calls.some(call => call[1]?.method === 'PUT')).toBe(true));
    const request = fetchMock.mock.calls.find(call => call[1]?.method === 'PUT')!;
    expect(request[0]).toContain('/api/admin/applications/client-one/administrative-access');
    expect(JSON.parse(request[1].body)).toEqual({ delegatedPermissions: ['users:read'], applicationPermissions: [], expectedRevision: null });
  });

  it('shows a denied approval and preserves the proposed ceiling for review', async () => {
    globalThis.fetch = vi.fn().mockImplementation(async (_url, options) => options?.method === 'PUT'
      ? new Response(JSON.stringify({ detail: 'Current unrestricted administrative authority is required.' }), { status: 403 })
      : new Response(JSON.stringify({ approved: false, delegatedPermissions: [], applicationPermissions: [], revision: null }), { status: 200 }));
    const user = userEvent.setup();
    renderManagementWeb(<AdministrativeAccessPanel applicationId="client-two" canWrite />);
    await screen.findByText('Not approved');
    await user.type(screen.getByLabelText('Machine permission ceiling'), 'audit-logs:read');
    await user.click(screen.getByRole('button', { name: 'Save administrative access' }));
    expect(await screen.findByText('Current unrestricted administrative authority is required.')).toBeInTheDocument();
    expect(screen.getByLabelText('Machine permission ceiling')).toHaveValue('audit-logs:read');
  });
});
