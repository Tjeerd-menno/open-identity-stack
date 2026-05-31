import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RegisteredApplicationList } from './RegisteredApplicationList';

const hooks = vi.hoisted(() => ({
  useRegisteredApplications: vi.fn(),
}));

const principalHooks = vi.hoisted(() => ({
  useGroup: vi.fn(),
  useUser: vi.fn(),
}));

vi.mock('../hooks', () => hooks);
vi.mock('@/features/groups/hooks/useGroup', () => ({ useGroup: principalHooks.useGroup }));
vi.mock('@/features/users/hooks/useUser', () => ({ useUser: principalHooks.useUser }));

const ownerId = '1f3d9f93-8f77-4e5d-8a4f-9a5ce6b56ee7';

function renderList() {
  render(
    <MemoryRouter>
      <RegisteredApplicationList />
    </MemoryRouter>
  );
}

describe('RegisteredApplicationList', () => {
  beforeEach(() => {
    vi.clearAllMocks();

    hooks.useRegisteredApplications.mockReturnValue({
      data: {
        items: [
          {
            id: 'application-1',
            applicationIdentifier: 'codex-inventory-mpo0a2p2',
            displayName: 'Codex Inventory mpo0a2p2',
            ownerId,
            status: 'Active',
            permissionCount: 3,
            createdAt: '2026-01-01T00:00:00Z',
            updatedAt: null,
          },
        ],
        page: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
      },
      isLoading: false,
    });

    principalHooks.useUser.mockReturnValue({
      data: {
        id: ownerId,
        email: 'admin@localhost.dev',
        displayName: 'Default Admin',
      },
      isLoading: false,
    });

    principalHooks.useGroup.mockReturnValue({
      data: undefined,
      isLoading: false,
    });
  });

  it('shows the owner display name instead of the raw id', () => {
    renderList();

    expect(screen.getByRole('columnheader', { name: 'Application' })).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Service' })).not.toBeInTheDocument();
    expect(screen.getByText('Default Admin')).toBeInTheDocument();
    expect(screen.getByText('admin@localhost.dev')).toBeInTheDocument();
    expect(screen.queryByText(ownerId)).not.toBeInTheDocument();
  });
});
