import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { RoleDetailPage } from './RoleDetailPage';
import type { CreateRoleRequest, Role } from '@/types';

const updateRole = vi.fn();

vi.mock('../hooks/useRole', () => ({
  useRole: () => ({
    data: {
      id: 'role-1',
      name: 'application-admin',
      displayName: 'Application Administrator',
      description: 'Manage applications',
      isSystemRole: true,
      isActive: true,
      permissions: ['applications:*'],
    } satisfies Role,
    isLoading: false,
    error: null,
  }),
}));

vi.mock('../hooks/useUpdateRole', () => ({
  useUpdateRole: () => ({
    mutate: updateRole,
    isPending: false,
  }),
}));

vi.mock('../components/RoleDetail', () => ({
  RoleDetail: ({ onEdit }: { onEdit: () => void }) => (
    <button type="button" onClick={onEdit}>
      Edit
    </button>
  ),
}));

vi.mock('../components/RoleForm', () => ({
  RoleForm: ({ onSubmit }: { onSubmit: (data: CreateRoleRequest) => void }) => (
    <button
      type="button"
      onClick={() =>
        onSubmit({
          name: 'application-admin',
          displayName: 'Application Administrator',
          description: 'Manage applications',
          permissions: ['applications:*'],
          acknowledgeWildcardGrant: true,
        })
      }
    >
      Submit role
    </button>
  ),
}));

describe('RoleDetailPage', () => {
  it('preserves wildcard acknowledgement when updating a role', async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter initialEntries={['/roles/role-1']}>
        <Routes>
          <Route path="/roles/:id" element={<RoleDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    await user.click(screen.getByRole('button', { name: /edit/i }));
    await user.click(screen.getByRole('button', { name: /submit role/i }));

    expect(updateRole).toHaveBeenCalledWith(
      {
        id: 'role-1',
        data: {
          displayName: 'Application Administrator',
          description: 'Manage applications',
          permissions: ['applications:*'],
          acknowledgeWildcardGrant: true,
        },
      },
      expect.any(Object)
    );
  });
});
