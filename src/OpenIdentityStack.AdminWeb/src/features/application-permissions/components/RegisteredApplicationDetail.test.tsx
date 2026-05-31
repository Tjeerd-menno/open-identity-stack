import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach } from 'vitest';
import { describe, expect, it, vi } from 'vitest';
import { RegisteredApplicationDetail } from './RegisteredApplicationDetail';

const hooks = vi.hoisted(() => ({
  useAddApplicationPermission: vi.fn(),
  useChangeApplicationLifecycle: vi.fn(),
  useManifestMutations: vi.fn(),
  useMaintainerMutations: vi.fn(),
  useRegisteredApplication: vi.fn(),
  useTransferApplicationOwnership: vi.fn(),
  useUpdateRegisteredApplication: vi.fn(),
}));

const principalHooks = vi.hoisted(() => ({
  useGroup: vi.fn(),
  useGroups: vi.fn(),
  useUser: vi.fn(),
  useUsers: vi.fn(),
}));

vi.mock('../hooks', () => hooks);
vi.mock('@/features/groups/hooks/useGroup', () => ({ useGroup: principalHooks.useGroup }));
vi.mock('@/features/groups/hooks/useGroups', () => ({ useGroups: principalHooks.useGroups }));
vi.mock('@/features/users/hooks/useUser', () => ({ useUser: principalHooks.useUser }));
vi.mock('@/features/users/hooks/useUsers', () => ({ useUsers: principalHooks.useUsers }));

const mutation = { mutate: vi.fn(), mutateAsync: vi.fn(), isPending: false };
const currentOwnerId = '11111111-1111-1111-1111-111111111111';
const maintainerUserId = '22222222-2222-2222-2222-222222222222';

function setupHooks(applicationPatch: Record<string, unknown> = {}) {
  hooks.useRegisteredApplication.mockReturnValue({
    data: {
      id: 'application-1',
      applicationIdentifier: 'patient-api',
      displayName: 'Patient API',
      description: '1.0.0',
      status: 'Active',
      ownerId: currentOwnerId,
      ownerType: 'User',
      manifestVersion: null,
      schemaVersion: null,
      manifestBaseUrl: null,
      concurrencyToken: 1,
      permissions: [],
      maintainers: [
        {
          id: 'maintainer-1',
          principalId: maintainerUserId,
          principalType: 'User',
          grantedBy: currentOwnerId,
          grantedAt: '2026-01-01T00:00:00Z',
        },
      ],
      ...applicationPatch,
    },
    isLoading: false,
  });
  principalHooks.useUsers.mockReturnValue({
    data: {
      items: [
        {
          id: 'user-1',
          email: 'alice@example.com',
          displayName: 'Alice Admin',
          status: 'Active',
          createdAt: '2026-01-01T00:00:00Z',
        },
      ],
    },
    isLoading: false,
  });
  principalHooks.useGroups.mockReturnValue({
    data: {
      items: [
        {
          id: 'group-1',
          name: 'Platform Team',
          description: 'Platform operators',
          memberCount: 2,
          mappingCount: 1,
          createdAt: '2026-01-01T00:00:00Z',
        },
      ],
    },
    isLoading: false,
  });
  principalHooks.useUser.mockImplementation((userId: string) => {
    const usersById = {
      [currentOwnerId]: {
        id: currentOwnerId,
        email: 'admin@localhost.dev',
        displayName: 'Default Admin',
      },
      [maintainerUserId]: {
        id: maintainerUserId,
        email: 'alice@example.com',
        displayName: 'Alice Admin',
      },
    };
    const user = usersById[userId as keyof typeof usersById];

    return {
      data: user ? {
        ...user,
        status: 'Active',
        mfaEnabled: false,
        lastLoginAt: null,
        createdAt: '2026-01-01T00:00:00Z',
        modifiedAt: null,
        profile: {
          givenName: null,
          familyName: null,
          middleName: null,
          nickname: null,
          preferredUsername: null,
          profile: null,
          picture: null,
          website: null,
          gender: null,
          birthdate: null,
          zoneInfo: null,
          locale: null,
        },
      } : null,
      isLoading: false,
    };
  });
  principalHooks.useGroup.mockReturnValue({ data: null, isLoading: false });
  hooks.useAddApplicationPermission.mockReturnValue(mutation);
  hooks.useChangeApplicationLifecycle.mockReturnValue(mutation);
  hooks.useTransferApplicationOwnership.mockReturnValue(mutation);
  hooks.useUpdateRegisteredApplication.mockReturnValue(mutation);
  hooks.useMaintainerMutations.mockReturnValue({ add: mutation, remove: mutation });
  hooks.useManifestMutations.mockReturnValue({
    preview: mutation,
    apply: mutation,
    previewRemote: mutation,
    applyRemote: mutation,
  });
}

describe('RegisteredApplicationDetail', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mutation.mutateAsync.mockResolvedValue(undefined);
  });

  it('uses a permission key placeholder that follows resource action syntax', () => {
    setupHooks();

    render(<RegisteredApplicationDetail id="application-1" />);

    expect(screen.getByLabelText('Permission name')).toHaveAttribute('placeholder', 'patient:read');
  });

  it('shows the current owner by username instead of raw id', () => {
    setupHooks();

    render(<RegisteredApplicationDetail id="application-1" />);

    expect(screen.getByText('Current owner')).toBeInTheDocument();
    expect(screen.getByText('Default Admin')).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'New owner' })).not.toBeInTheDocument();
    expect(screen.queryByText(currentOwnerId)).not.toBeInTheDocument();
  });

  it('selects a new owner with a single user and group typeahead', async () => {
    const user = userEvent.setup();
    setupHooks();

    render(<RegisteredApplicationDetail id="application-1" />);
    await user.click(screen.getByRole('button', { name: 'Edit' }));

    const ownerSelector = screen.getByRole('combobox', { name: 'New owner' });
    expect(screen.queryByLabelText('New owner ID')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('New owner type')).not.toBeInTheDocument();

    await user.click(ownerSelector);
    await user.type(ownerSelector, 'Alice');
    await user.click(screen.getByRole('option', { name: /Alice Admin alice@example.com User/i }));
    await user.click(screen.getByRole('button', { name: 'Transfer Ownership' }));

    expect(mutation.mutateAsync).toHaveBeenCalledWith({
      ownerId: 'user-1',
      ownerType: 'User',
      concurrencyToken: 1,
    });
  });

  it('closes the principal dropdown when focus moves outside the control', async () => {
    const user = userEvent.setup();
    setupHooks();

    render(<RegisteredApplicationDetail id="application-1" />);
    await user.click(screen.getByRole('button', { name: 'Edit' }));

    await user.click(screen.getByRole('combobox', { name: 'New owner' }));
    expect(screen.getByRole('listbox')).toBeInTheDocument();

    await user.click(screen.getByRole('heading', { name: 'Permissions' }));

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('shows maintainers by username instead of raw id', () => {
    setupHooks();

    render(<RegisteredApplicationDetail id="application-1" />);

    expect(screen.getByText('Alice Admin')).toBeInTheDocument();
    expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Remove' })).not.toBeInTheDocument();
    expect(screen.queryByText(maintainerUserId)).not.toBeInTheDocument();
  });

  it('groups ownership and maintainer controls before the permissions list', () => {
    setupHooks();

    render(<RegisteredApplicationDetail id="application-1" />);

    const permissionsHeading = screen.getByRole('heading', { name: 'Permissions' });
    const ownershipHeading = screen.getByRole('heading', { name: 'Ownership' });
    const maintainersHeading = screen.getByRole('heading', { name: 'Maintainers' });

    expect(ownershipHeading.compareDocumentPosition(permissionsHeading) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(maintainersHeading.compareDocumentPosition(permissionsHeading) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('renders application-level data in a named details panel separate from permissions', () => {
    setupHooks();

    render(<RegisteredApplicationDetail id="application-1" />);

    const applicationDetails = screen.getByRole('region', { name: 'Application details' });

    expect(applicationDetails).toContainElement(screen.getByRole('heading', { name: 'Patient API' }));
    expect(applicationDetails).toContainElement(screen.getByText('Current owner'));
    expect(applicationDetails).toContainElement(screen.getByRole('heading', { name: 'Maintainers' }));
    expect(applicationDetails).not.toContainElement(screen.getByRole('heading', { name: 'Permissions' }));
  });

  it('does not display a version-shaped value as the application description', () => {
    setupHooks();

    render(<RegisteredApplicationDetail id="application-1" />);

    const applicationDetails = screen.getByRole('region', { name: 'Application details' });

    expect(applicationDetails).not.toHaveTextContent('Description');
    expect(applicationDetails).not.toHaveTextContent('1.0.0');
  });

  it('keeps application detail actions hidden until edit mode', async () => {
    const user = userEvent.setup();
    setupHooks();

    render(<RegisteredApplicationDetail id="application-1" />);

    expect(screen.queryByText('Lifecycle')).not.toBeInTheDocument();
    expect(screen.queryByText('Current status: Active')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Enable' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Disable' })).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'New owner' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Transfer Ownership' })).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'New maintainer' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add Maintainer' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Edit' }));

    expect(screen.getByRole('button', { name: 'Disable' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'New owner' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Transfer Ownership' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'New maintainer' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add Maintainer' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Remove' })).toBeInTheDocument();
  });

  it('does not expose manual edit controls for manifest-backed applications', async () => {
    const user = userEvent.setup();
    setupHooks({ manifestBaseUrl: 'https://patient.example/api' });

    render(<RegisteredApplicationDetail id="application-1" />);

    expect(screen.queryByRole('textbox', { name: 'Permission name' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Edit' }));

    expect(screen.queryByRole('button', { name: 'Disable' })).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'New owner' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Transfer Ownership' })).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'New maintainer' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add Maintainer' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Remove' })).not.toBeInTheDocument();
  });

  it('allows editing only the manifest base URL for manifest-backed applications', async () => {
    const user = userEvent.setup();
    setupHooks({
      description: 'Patient data app',
      manifestBaseUrl: 'https://patient.example/api',
    });

    render(<RegisteredApplicationDetail id="application-1" />);
    await user.click(screen.getByRole('button', { name: 'Edit' }));

    const manifestBaseUrl = screen.getByRole('textbox', { name: 'Manifest Base URL' });
    await user.clear(manifestBaseUrl);
    await user.type(manifestBaseUrl, 'https://permissions.example.com/root/');
    await user.click(screen.getByRole('button', { name: 'Save URL' }));

    expect(mutation.mutateAsync).toHaveBeenCalledWith({
      displayName: 'Patient API',
      description: 'Patient data app',
      manifestBaseUrl: 'https://permissions.example.com/root/',
      concurrencyToken: 1,
    });
  });

  it('does not show inline manifest update controls', () => {
    setupHooks();

    render(<RegisteredApplicationDetail id="application-1" />);

    expect(screen.queryByRole('heading', { name: 'Manifest Update' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Manifest JSON')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Preview Manifest' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Apply Manifest' })).not.toBeInTheDocument();
  });

  it('selects a maintainer with a single user and group typeahead', async () => {
    const user = userEvent.setup();
    setupHooks();

    render(<RegisteredApplicationDetail id="application-1" />);
    await user.click(screen.getByRole('button', { name: 'Edit' }));

    const maintainerSelector = screen.getByRole('combobox', { name: 'New maintainer' });
    expect(screen.queryByLabelText('Maintainer principal ID')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Maintainer principal type')).not.toBeInTheDocument();

    await user.click(maintainerSelector);
    await user.type(maintainerSelector, 'Alice');
    await user.click(screen.getByRole('option', { name: /Alice Admin alice@example.com User/i }));
    await user.click(screen.getByRole('button', { name: 'Add Maintainer' }));

    expect(mutation.mutateAsync).toHaveBeenCalledWith({
      principalId: 'user-1',
      principalType: 'User',
      concurrencyToken: 1,
    });
  });
});
