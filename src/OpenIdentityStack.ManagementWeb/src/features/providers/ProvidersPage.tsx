import { Alert, Badge, Button, Group, SimpleGrid, Stack, Text, ThemeIcon, Title } from '@mantine/core';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { EntityActionGroup } from '@/components/EntityActionMenu';
import { FoundationTable, type FoundationColumn } from '@/components/FoundationTable';
import { ProvidersIcon } from '@/components/IamIcons';
import { PageHeader, PageToolbar } from '@/components/PagePrimitives';
import { SettingsPage } from '@/features/settings/SettingsPage';
import { getApiErrorMessage } from '@/lib/admin-api';
import { hasPermission } from '@/lib/permissions';
import { ProviderForm } from './ProviderForm';
import {
  useCreateProvider,
  useDeleteProvider,
  useProvider,
  useProviders,
  useToggleProvider,
  useUpdateProvider,
} from './providers-hooks';
import type { CreateProviderRequest, Provider, ProviderStatus, UpdateProviderRequest } from './providers-api';

type ProvidersPageProps = {
  permissions?: string[];
};

export function ProvidersPage({ permissions = ['*'] }: ProvidersPageProps) {
  const location = useLocation();
  const { id } = useParams();
  const pathProviderId = getProviderIdFromPath(location.pathname);

  if (location.pathname === '/providers/settings') {
    return <SettingsPage />;
  }

  if (location.pathname.endsWith('/new')) {
    return <CreateProviderView permissions={permissions} />;
  }

  if (location.pathname.endsWith('/edit') && (id || pathProviderId)) {
    return <EditProviderView providerId={id ?? pathProviderId ?? ''} permissions={permissions} />;
  }

  if (id || pathProviderId) {
    return <ProviderDetailView providerId={id ?? pathProviderId ?? ''} permissions={permissions} />;
  }

  return <ProviderListView permissions={permissions} />;
}

function getProviderIdFromPath(pathname: string) {
  const editMatch = /^\/providers\/([^/]+)\/edit$/.exec(pathname);
  if (editMatch) {
    return editMatch[1];
  }

  return /^\/providers\/([^/]+)$/.exec(pathname)?.[1];
}

function ProviderListView({ permissions }: Required<ProvidersPageProps>) {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [submittedSearch, setSubmittedSearch] = useState('');
  const [providerToDelete, setProviderToDelete] = useState<Provider | null>(null);
  const searchMounted = useRef(false);
  const providers = useProviders(true);
  const deleteProvider = useDeleteProvider(providerToDelete?.id ?? '');
  const canWrite = hasPermission(permissions, 'providers:write');
  const canDelete = hasPermission(permissions, 'providers:delete');
  const canManageSettings = hasPermission(permissions, 'system:settings');

  useEffect(() => {
    if (!searchMounted.current) {
      searchMounted.current = true;
      return;
    }

    const timer = window.setTimeout(() => setSubmittedSearch(search), 300);
    return () => window.clearTimeout(timer);
  }, [search]);

  const filteredProviders = useMemo(() => {
    const term = submittedSearch.trim().toLowerCase();
    if (!term) {
      return providers.data ?? [];
    }

    return (providers.data ?? []).filter((provider) =>
      provider.name.toLowerCase().includes(term)
      || provider.displayName.toLowerCase().includes(term)
      || provider.authority.toLowerCase().includes(term)
    );
  }, [providers.data, submittedSearch]);

  const columns: FoundationColumn<Provider>[] = [
    {
      header: 'Display name',
      cell: (provider) => (
        <Group gap="sm" wrap="nowrap">
          <ThemeIcon color={provider.status === 'Active' ? 'blue' : 'gray'} variant="light" size={34} radius="md">
            <ProvidersIcon />
          </ThemeIcon>
          <Stack gap={0} style={{ minWidth: 0 }}>
            <Text fw={600} style={{ color: 'var(--mw-text-strong)' }}>{provider.displayName}</Text>
            <Text size="sm" c="dimmed">{provider.name}</Text>
          </Stack>
        </Group>
      ),
    },
    {
      header: 'Authority',
      cell: (provider) => <Text ff="monospace" size="sm" c="dimmed">{provider.authority}</Text>,
    },
    {
      header: 'Type',
      cell: () => <Badge variant="light">OIDC</Badge>,
    },
    {
      header: 'Status',
      cell: (provider) => <ProviderStatusBadge status={provider.status} />,
    },
    {
      header: 'JIT Provisioning',
      cell: (provider) => <Badge color={provider.jitProvisioningEnabled ? 'green' : 'gray'} variant="light">{provider.jitProvisioningEnabled ? 'Enabled' : 'Disabled'}</Badge>,
    },
    {
      header: 'Actions',
      align: 'right',
      cell: (provider) => (
        <EntityActionGroup
          actions={[
            { label: `View ${provider.displayName}`, onClick: () => navigate(`/providers/${provider.id}`) },
            ...(canWrite ? [{ label: `Edit ${provider.displayName}`, onClick: () => navigate(`/providers/${provider.id}/edit`) }] : []),
            ...(canDelete ? [{ label: `Delete ${provider.name}`, color: 'red' as const, onClick: () => setProviderToDelete(provider) }] : []),
          ]}
        />
      ),
    },
  ];

  return (
    <Stack gap="lg">
      <PageHeader
        title="Identity Providers"
        description="Manage OIDC identity providers and JIT provisioning."
        actions={(
          <Group>
            {canManageSettings && (
              <Button component={Link} to="/providers/settings" variant="default">
                Authentication settings
              </Button>
            )}
            {canWrite && <Button onClick={() => navigate('/providers/new')}>New provider</Button>}
          </Group>
        )}
      />

      {(!canWrite || !canDelete) && <Alert color="blue">Read-only access. Some provider actions require additional permissions.</Alert>}

      <PageToolbar
        searchLabel="Search providers"
        searchPlaceholder="Search providers by name or authority..."
        searchValue={search}
        resultCount={filteredProviders.length}
        onSearchChange={setSearch}
        onClear={() => {
          setSearch('');
          setSubmittedSearch('');
        }}
      />

      <FoundationTable
        columns={columns}
        data={filteredProviders}
        isLoading={providers.isLoading}
        error={providers.isError ? getApiErrorMessage(providers.error) : null}
        emptyMessage="No identity providers found"
      />

      <ConfirmDialog
        opened={providerToDelete !== null}
        onOpenChange={(opened) => !opened && setProviderToDelete(null)}
        title="Delete provider"
        message={`Delete ${providerToDelete?.displayName ?? 'this provider'}? This action cannot be undone and may affect users who authenticate through this provider.`}
        confirmLabel={`Delete ${providerToDelete?.name ?? 'provider'}`}
        loading={deleteProvider.isPending}
        onConfirm={async () => {
          await deleteProvider.mutateAsync();
          setProviderToDelete(null);
        }}
      />
    </Stack>
  );
}

function CreateProviderView({ permissions }: Required<ProvidersPageProps>) {
  const navigate = useNavigate();
  const createProvider = useCreateProvider();

  if (!hasPermission(permissions, 'providers:write')) {
    return <Alert color="blue">Read-only access. Provider changes require providers:write.</Alert>;
  }

  return (
    <Stack gap="lg">
      <div>
        <Title order={1}>Create provider</Title>
        <Text c="dimmed">Register an OIDC identity provider.</Text>
      </div>
      <ProviderForm
        mode="create"
        loading={createProvider.isPending}
        error={createProvider.error}
        onCancel={() => navigate('/providers')}
        onSubmit={async (data) => {
          const provider = await createProvider.mutateAsync(data as CreateProviderRequest);
          navigate(`/providers/${provider.id}`);
        }}
      />
    </Stack>
  );
}

function EditProviderView({ providerId, permissions }: { providerId: string; permissions: string[] }) {
  const navigate = useNavigate();
  const provider = useProvider(providerId);
  const updateProvider = useUpdateProvider(providerId);

  if (!hasPermission(permissions, 'providers:write')) {
    return <Alert color="blue">Read-only access. Provider changes require providers:write.</Alert>;
  }

  if (provider.isLoading) {
    return <Text>Loading provider</Text>;
  }

  if (provider.isError) {
    return <Alert color="red">{getApiErrorMessage(provider.error)}</Alert>;
  }

  if (!provider.data) {
    return <Alert color="red">Provider not found.</Alert>;
  }

  return (
    <Stack gap="lg">
      <div>
        <Title order={1}>Edit provider</Title>
        <Text c="dimmed">{provider.data.displayName}</Text>
      </div>
      <ProviderForm
        mode="edit"
        provider={provider.data}
        loading={updateProvider.isPending}
        error={updateProvider.error}
        onCancel={() => navigate(`/providers/${providerId}`)}
        onSubmit={async (data) => {
          await updateProvider.mutateAsync(data as UpdateProviderRequest);
          navigate(`/providers/${providerId}`);
        }}
      />
    </Stack>
  );
}

function ProviderDetailView({ providerId, permissions }: { providerId: string; permissions: string[] }) {
  const navigate = useNavigate();
  const provider = useProvider(providerId);
  const deleteProvider = useDeleteProvider(providerId);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const canWrite = hasPermission(permissions, 'providers:write');
  const canDelete = hasPermission(permissions, 'providers:delete');
  const toggleProvider = useToggleProvider(providerId, provider.data?.status === 'Active' ? 'Disabled' : 'Active');

  if (provider.isLoading) {
    return <Text>Loading provider</Text>;
  }

  if (provider.isError) {
    return <Alert color="red">{getApiErrorMessage(provider.error)}</Alert>;
  }

  if (!provider.data) {
    return <Alert color="red">Provider not found.</Alert>;
  }

  const isActive = provider.data.status === 'Active';

  return (
    <Stack gap="lg" role="region" aria-label="Provider details">
      <Group justify="space-between" align="flex-start">
        <div>
          <Title order={1}>{provider.data.displayName}</Title>
          <Text c="dimmed">{provider.data.name}</Text>
        </div>
        <Group>
          <Button variant="default" onClick={() => navigate('/providers')}>Back to Providers</Button>
          {canWrite && (
            <>
              <Button
                variant={isActive ? 'light' : 'filled'}
                color={isActive ? 'red' : 'green'}
                loading={toggleProvider.isPending}
                onClick={() => void toggleProvider.mutateAsync()}
              >
                {isActive ? 'Disable provider' : 'Enable provider'}
              </Button>
              <Button onClick={() => navigate(`/providers/${providerId}/edit`)}>Edit provider</Button>
            </>
          )}
          {canDelete && (
            <Button color="red" variant="light" onClick={() => setConfirmDelete(true)}>
              Delete provider
            </Button>
          )}
        </Group>
      </Group>

      {(!canWrite || !canDelete) && <Alert color="blue">Read-only access. Some provider actions require additional permissions.</Alert>}

      <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="lg">
        <section aria-label="Provider information">
          <Title order={2} size="h3">Provider Information</Title>
          <Stack gap={4} mt="sm">
            <Group gap="xs">
              <Text size="sm">Status:</Text>
              <ProviderStatusBadge status={provider.data.status} />
            </Group>
            <Group gap="xs">
              <Text size="sm">Type:</Text>
              <Badge variant="light">OIDC</Badge>
            </Group>
            <Text size="sm">Authority: {provider.data.authority}</Text>
            <Text size="sm">Client ID: {provider.data.clientId}</Text>
            <Text size="sm">Created: {new Date(provider.data.createdAt).toLocaleString()}</Text>
          </Stack>
        </section>

        <section aria-label="Provider configuration">
          <Title order={2} size="h3">Configuration</Title>
          <Stack gap={4} mt="sm">
            <Group gap="xs">
              <Text size="sm">Scopes:</Text>
              {provider.data.scopes.map((scope) => <Badge key={scope} variant="outline">{scope}</Badge>)}
            </Group>
            <Group gap="xs">
              <Text size="sm">JIT Provisioning:</Text>
              <Badge color={provider.data.jitProvisioningEnabled ? 'green' : 'gray'} variant="light">
                {provider.data.jitProvisioningEnabled ? 'Enabled' : 'Disabled'}
              </Badge>
            </Group>
          </Stack>
        </section>
      </SimpleGrid>

      <ConfirmDialog
        opened={confirmDelete}
        onOpenChange={setConfirmDelete}
        title="Delete provider"
        message={`Delete ${provider.data.displayName}? This action cannot be undone and may affect users who authenticate through this provider.`}
        confirmLabel={`Delete ${provider.data.name}`}
        loading={deleteProvider.isPending}
        onConfirm={async () => {
          await deleteProvider.mutateAsync();
          navigate('/providers');
        }}
      />
    </Stack>
  );
}

function ProviderStatusBadge({ status }: { status: ProviderStatus }) {
  return <Badge color={status === 'Active' ? 'green' : 'gray'} variant="light">{status === 'Active' ? 'Active' : 'Disabled'}</Badge>;
}
