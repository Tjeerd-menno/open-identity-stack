import { act, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderManagementWeb } from '@/test/render';
import { AdministrativeApprovalDialog } from './AdministrativeApprovalDialog';
import { api } from '@/lib/api';

const originalFetch = globalThis.fetch;
afterEach(() => { globalThis.fetch = originalFetch; });

describe('AdministrativeApprovalDialog', () => {
  it('offers fresh sign-in when authentication expires during acknowledgement', async () => {
    const user = userEvent.setup();
    const reauthenticate = vi.fn().mockResolvedValue(undefined);
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ errorCode: 'Forbidden.AdministrativeApproval.AcknowledgementRequired' }), { status: 403 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ errorCode: 'Forbidden.AdministrativeApproval.ReauthenticationRequired' }), { status: 403 }));
    globalThis.fetch = fetchMock;
    renderManagementWeb(<AdministrativeApprovalDialog onReauthenticate={reauthenticate} />);
    let pending!: Promise<unknown>;
    await act(async () => { pending = api.users.assignUserRole('user', 'role').catch(error => error); });
    await user.click(await screen.findByRole('checkbox'));
    await user.click(screen.getByRole('button', { name: 'Approve operation' }));
    await user.click(await screen.findByRole('button', { name: 'Sign in again' }));
    expect(await pending).toMatchObject({ status: 403 });
    expect(reauthenticate).toHaveBeenCalledOnce();
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('describes cutover approval without claiming to grant access', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ errorCode: 'Forbidden.AdministrativeApproval.AcknowledgementRequired' }), { status: 403 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ operationId: 'cutover', tokens: 2, grants: 1, sessions: 1 }), { status: 200 }));
    globalThis.fetch = fetchMock;
    renderManagementWeb(<AdministrativeApprovalDialog onReauthenticate={vi.fn()} />);
    let pending!: Promise<unknown>;
    await act(async () => { pending = api.cutover.execute('cutover'); });
    expect(await screen.findByRole('dialog', { name: 'Approve administrative operation' })).toBeVisible();
    expect(screen.queryByText(/access this operation grants/i)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Approve operation' })).toBeDisabled();
    await user.click(screen.getByRole('checkbox', { name: 'I acknowledge the effects of this operation.' }));
    await user.click(screen.getByRole('button', { name: 'Approve operation' }));
    await pending;
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(new Headers(fetchMock.mock.calls[1][1].headers).get('X-OIS-Administrative-Approval')).toBe('acknowledge');
  });

  it('requires acknowledgement before retrying the pending operation', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ errorCode: 'Forbidden.AdministrativeApproval.AcknowledgementRequired' }), { status: 403 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    globalThis.fetch = fetchMock;
    renderManagementWeb(<AdministrativeApprovalDialog onReauthenticate={vi.fn()} />);
    let pending!: Promise<unknown>;
    await act(async () => { pending = api.users.assignUserRole('user', 'role'); });

    expect(await screen.findByRole('button', { name: 'Approve operation' })).toBeDisabled();
    await user.click(screen.getByRole('checkbox'));
    await user.click(screen.getByRole('button', { name: 'Approve operation' }));
    await pending;
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(new Headers(fetchMock.mock.calls[1][1].headers).get('X-OIS-Administrative-Approval')).toBe('acknowledge');
  });

  it('cancels without resubmitting the operation', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      errorCode: 'Forbidden.AdministrativeApproval.AcknowledgementRequired',
    }), { status: 403 }));
    globalThis.fetch = fetchMock;
    renderManagementWeb(<AdministrativeApprovalDialog onReauthenticate={vi.fn()} />);
    let pending!: Promise<unknown>;
    await act(async () => { pending = api.users.assignUserRole('user', 'role').catch(error => error); });
    await user.click(await screen.findByRole('button', { name: 'Cancel' }));
    expect(await pending).toMatchObject({ status: 403 });
    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it('requests fresh sign-in without replaying an expired approval attempt', async () => {
    const user = userEvent.setup();
    const reauthenticate = vi.fn().mockResolvedValue(undefined);
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      errorCode: 'Forbidden.AdministrativeApproval.ReauthenticationRequired',
    }), { status: 403 }));
    globalThis.fetch = fetchMock;
    renderManagementWeb(<AdministrativeApprovalDialog onReauthenticate={reauthenticate} />);
    let pending!: Promise<unknown>;
    await act(async () => { pending = api.users.assignUserRole('user', 'role').catch(error => error); });
    await user.click(await screen.findByRole('button', { name: 'Sign in again' }));
    await pending;
    expect(reauthenticate).toHaveBeenCalledOnce();
    expect(fetchMock).toHaveBeenCalledOnce();
  });
});
