import {
  Alert,
  Badge,
  Button,
  Group,
  NativeSelect,
  Paper,
  Stack,
  Text,
  Textarea,
  TextInput,
  Title,
} from '@mantine/core';
import { FormEvent, useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router';
import { FoundationTable, type FoundationColumn } from '@/components/FoundationTable';
import { getApiErrorMessage } from '@/lib/admin-api';
import { hasPermission } from '@/lib/permissions';
import {
  useAddApplicationPermission,
  useApplicationPermissionDiagnostics,
  useApplicationPermissionHistory,
  useAssignablePermissionCatalog,
  useChangeApplicationLifecycle,
  useImportPermissionManifestFromEndpoint,
  useMaintainerMutations,
  useRegisteredApplication,
  useRegisteredApplications,
  useRegisterPermissionManifest,
  useTransferApplicationOwnership,
} from './application-permissions-hooks';
import type {
  PermissionManifestPermission,
  PermissionManifestRequest,
  PrincipalType,
  RegisteredApplicationListItem,
} from './application-permissions-api';

const pageSize = 20;

type ApplicationPermissionsPageProps = {
  permissions?: string[];
};

export function ApplicationPermissionsPage({ permissions = ['*'] }: ApplicationPermissionsPageProps) {
  const location = useLocation();
  const { id } = useParams();
  const pathApplicationId = /^\/application-permissions\/([^/]+)$/.exec(location.pathname)?.[1];

  if (location.pathname.endsWith('/new')) {
    return <RegisterApplicationView permissions={permissions} />;
  }

  if (id || pathApplicationId) {
    return <RegisteredApplicationDetailView applicationId={id ?? pathApplicationId ?? ''} permissions={permissions} />;
  }

  return <RegisteredApplicationListView permissions={permissions} />;
}

function RegisteredApplicationListView({ permissions }: Required<ApplicationPermissionsPageProps>) {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [submittedSearch, setSubmittedSearch] = useState('');
  const searchMounted = useRef(false);
  const applications = useRegisteredApplications({ page, pageSize, search: submittedSearch || undefined });
  const canWrite = hasPermission(permissions, 'application-permissions:write');

  useEffect(() => {
    if (!searchMounted.current) {
      searchMounted.current = true;
      return;
    }

    const timer = window.setTimeout(() => {
      setSubmittedSearch(search);
      setPage(1);
    }, 300);

    return () => window.clearTimeout(timer);
  }, [search]);

  const columns: FoundationColumn<RegisteredApplicationListItem>[] = [
    { header: 'Application', accessorKey: 'applicationIdentifier' },
    { header: 'Display Name', accessorKey: 'displayName' },
    {
      header: 'Owner',
      cell: (application) => `${application.ownerId} (${application.ownerType})`,
    },
    {
      header: 'Permissions',
      cell: (application) => <Badge variant="light">{application.permissionCount}</Badge>,
    },
    {
      header: 'Status',
      cell: (application) => <ApplicationPermissionStatusBadge status={application.status} />,
    },
    {
      header: 'Actions',
      cell: (application) => (
        <Button
          variant="subtle"
          size="xs"
          aria-label={`View ${application.displayName}`}
          onClick={() => navigate(`/application-permissions/${application.id}`)}
        >
          View
        </Button>
      ),
    },
  ];

  return (
    <Stack gap="lg">
      <Group justify="space-between" align="flex-start">
        <div>
          <Title order={1}>Permissions</Title>
          <Text c="dimmed">Manage application-owned permission registries and assignments.</Text>
        </div>
        {canWrite && <Button onClick={() => navigate('/application-permissions/new')}>Add Application</Button>}
      </Group>

      {!canWrite && <Alert color="blue">Read-only access. Application permission changes require additional permissions.</Alert>}

      <TextInput
        label="Search applications"
        maw={420}
        placeholder="Search applications..."
        value={search}
        onChange={(event) => setSearch(event.currentTarget.value)}
      />

      <FoundationTable
        columns={columns}
        data={applications.data?.items ?? []}
        isLoading={applications.isLoading}
        error={applications.isError ? getApiErrorMessage(applications.error) : null}
        emptyMessage="No registered applications found"
        pagination={{
          page,
          pageSize,
          totalCount: applications.data?.totalCount ?? 0,
          totalPages: applications.data?.totalPages ?? 0,
          onPageChange: setPage,
        }}
      />
    </Stack>
  );
}

function RegisterApplicationView({ permissions }: Required<ApplicationPermissionsPageProps>) {
  const navigate = useNavigate();
  const register = useRegisterPermissionManifest();
  const importEndpoint = useImportPermissionManifestFromEndpoint();

  if (!hasPermission(permissions, 'application-permissions:write')) {
    return <Alert color="blue">Read-only access. Registering applications requires application-permissions:write.</Alert>;
  }

  return (
    <Stack gap="lg">
      <div>
        <Title order={1}>Add Application</Title>
        <Text c="dimmed">Register a manual permission manifest or import a well-known permissions endpoint.</Text>
      </div>
      <RegisterApplicationForm
        loading={register.isPending}
        importing={importEndpoint.isPending}
        error={register.error ?? importEndpoint.error}
        onCancel={() => navigate('/application-permissions')}
        onSubmit={async (data) => {
          const response = await register.mutateAsync(data);
          navigate(`/application-permissions/${response.id || data.manifest.application.id}`);
        }}
        onImportEndpoint={async (endpoint) => {
          const response = await importEndpoint.mutateAsync(endpoint);
          navigate(`/application-permissions/${response.id || 'imported'}`);
        }}
      />
    </Stack>
  );
}

function RegisterApplicationForm({
  loading,
  importing,
  error,
  onCancel,
  onSubmit,
  onImportEndpoint,
}: {
  loading: boolean;
  importing: boolean;
  error: unknown;
  onCancel: () => void;
  onSubmit: (data: PermissionManifestRequest) => Promise<void>;
  onImportEndpoint: (endpoint: string) => Promise<void>;
}) {
  const [endpoint, setEndpoint] = useState('');
  const [applicationId, setApplicationId] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [description, setDescription] = useState('');
  const [version, setVersion] = useState('');
  const [ownerId, setOwnerId] = useState('');
  const [ownerType, setOwnerType] = useState<'user' | 'group'>('user');
  const [permissions, setPermissions] = useState<PermissionManifestPermission[]>([
    { key: '', displayName: '', description: '', category: '' },
  ]);
  const [validationError, setValidationError] = useState<string | null>(null);

  function updatePermission(index: number, patch: Partial<PermissionManifestPermission>) {
    setPermissions((current) => current.map((permission, currentIndex) =>
      currentIndex === index ? { ...permission, ...patch } : permission
    ));
  }

  async function submitManifest(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const keys = permissions.map((permission) => permission.key.trim()).filter(Boolean);
    if (new Set(keys).size !== keys.length) {
      setValidationError('Permission keys must be unique.');
      return;
    }

    setValidationError(null);
    await onSubmit({
      manifest: {
        schemaVersion: '1.0.0',
        application: {
          id: applicationId,
          displayName,
          description: description || null,
          version,
        },
        permissions: permissions.map((permission) => ({
          key: permission.key,
          displayName: permission.displayName,
          description: permission.description || null,
          category: permission.category || null,
        })),
      },
      ownerId,
      ownerType,
      manifestBaseUrl: null,
    });
  }

  return (
    <Stack gap="lg">
      {error && <Alert color="red">{getApiErrorMessage(error)}</Alert>}
      <Paper withBorder p="md" radius="sm">
        <form
          onSubmit={(event) => {
            event.preventDefault();
            void onImportEndpoint(endpoint);
          }}
        >
          <Group align="end">
            <TextInput
              label="Well-known permissions endpoint"
              placeholder="https://patient.example/.well-known/permissions"
              value={endpoint}
              onChange={(event) => setEndpoint(event.currentTarget.value)}
              type="url"
              style={{ flex: 1 }}
            />
            <Button type="submit" variant="light" loading={importing} disabled={!endpoint.trim()}>
              Import Endpoint
            </Button>
          </Group>
        </form>
      </Paper>

      <form onSubmit={submitManifest}>
        <Stack gap="lg">
          <Paper withBorder p="md" radius="sm">
            <Title order={2} size="h3">Application</Title>
            <Group grow align="flex-start" mt="md">
              <TextInput aria-label="Application ID" label="Application ID" value={applicationId} onChange={(event) => setApplicationId(event.currentTarget.value)} required />
              <TextInput aria-label="Display Name" label="Display Name" value={displayName} onChange={(event) => setDisplayName(event.currentTarget.value)} required />
              <TextInput aria-label="Version" label="Version" value={version} onChange={(event) => setVersion(event.currentTarget.value)} required />
            </Group>
            <Textarea aria-label="Description" label="Description" mt="md" value={description} onChange={(event) => setDescription(event.currentTarget.value)} />
            <Group grow align="flex-start" mt="md">
              <TextInput aria-label="Owner ID" label="Owner ID" value={ownerId} onChange={(event) => setOwnerId(event.currentTarget.value)} required />
              <NativeSelect
                label="Owner Type"
                value={ownerType}
                onChange={(event) => setOwnerType(event.currentTarget.value as 'user' | 'group')}
                data={[
                  { value: 'user', label: 'User' },
                  { value: 'group', label: 'Group' },
                ]}
              />
            </Group>
          </Paper>

          <Paper withBorder p="md" radius="sm">
            <Group justify="space-between" align="center">
              <Title order={2} size="h3">Permissions</Title>
              <Button
                type="button"
                variant="light"
                onClick={() => setPermissions((current) => [...current, { key: '', displayName: '', description: '', category: '' }])}
              >
                Add Permission
              </Button>
            </Group>
            <Stack gap="md" mt="md">
              {permissions.map((permission, index) => (
                <Paper key={index} withBorder p="sm" radius="sm">
                  <Group grow align="flex-start">
                    <TextInput aria-label={`Permission key ${index + 1}`} label={`Permission key ${index + 1}`} value={permission.key} onChange={(event) => updatePermission(index, { key: event.currentTarget.value })} required />
                    <TextInput aria-label={`Permission display name ${index + 1}`} label={`Permission display name ${index + 1}`} value={permission.displayName} onChange={(event) => updatePermission(index, { displayName: event.currentTarget.value })} required />
                    <TextInput aria-label={`Permission category ${index + 1}`} label={`Permission category ${index + 1}`} value={permission.category ?? ''} onChange={(event) => updatePermission(index, { category: event.currentTarget.value })} />
                  </Group>
                  <Textarea aria-label={`Permission description ${index + 1}`} label={`Permission description ${index + 1}`} mt="sm" value={permission.description ?? ''} onChange={(event) => updatePermission(index, { description: event.currentTarget.value })} />
                  <Button
                    type="button"
                    variant="subtle"
                    color="red"
                    mt="sm"
                    disabled={permissions.length === 1}
                    onClick={() => setPermissions((current) => current.filter((_, currentIndex) => currentIndex !== index))}
                  >
                    Remove
                  </Button>
                </Paper>
              ))}
            </Stack>
            {validationError && <Alert color="red" mt="md">{validationError}</Alert>}
          </Paper>

          <Group justify="flex-end">
            <Button type="button" variant="default" onClick={onCancel}>Cancel</Button>
            <Button type="submit" loading={loading}>Add Application</Button>
          </Group>
        </Stack>
      </form>
    </Stack>
  );
}

function RegisteredApplicationDetailView({ applicationId, permissions }: { applicationId: string; permissions: string[] }) {
  const application = useRegisteredApplication(applicationId);
  const catalog = useAssignablePermissionCatalog({ page: 1, pageSize: 50 });
  const history = useApplicationPermissionHistory(application.data?.applicationIdentifier);
  const diagnostics = useApplicationPermissionDiagnostics();
  const lifecycle = useChangeApplicationLifecycle(applicationId);
  const transferOwnership = useTransferApplicationOwnership(applicationId);
  const addPermission = useAddApplicationPermission(applicationId);
  const maintainers = useMaintainerMutations(applicationId);
  const [editing, setEditing] = useState(false);
  const [ownerId, setOwnerId] = useState('');
  const [ownerType, setOwnerType] = useState<PrincipalType>('User');
  const [maintainerId, setMaintainerId] = useState('');
  const [maintainerType, setMaintainerType] = useState<PrincipalType>('User');
  const [permissionName, setPermissionName] = useState('');
  const [permissionCategory, setPermissionCategory] = useState('');
  const [permissionDescription, setPermissionDescription] = useState('');
  const canWrite = hasPermission(permissions, 'application-permissions:write');
  const canAdmin = hasPermission(permissions, 'application-permissions:admin') || canWrite;

  if (application.isLoading) {
    return <Text c="dimmed">Loading application permission registry</Text>;
  }

  if (application.isError) {
    return <Alert color="red">{getApiErrorMessage(application.error)}</Alert>;
  }

  if (!application.data) {
    return <Alert color="red">Application permission registry not found.</Alert>;
  }

  const isManifestBacked = Boolean(application.data.manifestBaseUrl);
  const concurrencyToken = application.data.concurrencyToken;

  async function toggleLifecycle() {
    await lifecycle.mutateAsync({
      status: application.data.status === 'Active' ? 'Disabled' : 'Active',
      acknowledgeDependencies: true,
      concurrencyToken,
    });
  }

  async function submitOwnership(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await transferOwnership.mutateAsync({ ownerId, ownerType, concurrencyToken });
    setOwnerId('');
  }

  async function submitMaintainer(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await maintainers.add.mutateAsync({ principalId: maintainerId, principalType: maintainerType, concurrencyToken });
    setMaintainerId('');
  }

  async function submitPermission(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await addPermission.mutateAsync({
      permissionKey: permissionName,
      displayName: permissionName,
      description: permissionDescription || null,
      category: permissionCategory || null,
      concurrencyToken,
    });
    setPermissionName('');
    setPermissionCategory('');
    setPermissionDescription('');
  }

  return (
    <Stack gap="lg" role="region" aria-label="Application permission details">
      <Group justify="space-between" align="flex-start">
        <div>
          <Title order={1}>{application.data.displayName}</Title>
          <Text c="dimmed">{application.data.applicationIdentifier}</Text>
        </div>
        <Group>
          <ApplicationPermissionStatusBadge status={application.data.status} />
          {canWrite && <Button variant="light" onClick={() => setEditing((value) => !value)}>{editing ? 'Done' : 'Edit'}</Button>}
        </Group>
      </Group>

      <Paper withBorder p="md" radius="sm">
        <Stack gap="xs">
          <Text size="sm">Description: {application.data.description || 'Not set'}</Text>
          <Text size="sm">Manifest Version: {application.data.manifestVersion || 'Not set'}</Text>
          <Text size="sm">Schema Version: {application.data.schemaVersion || 'Not set'}</Text>
          {application.data.manifestBaseUrl && <Text size="sm">Manifest Base URL: {application.data.manifestBaseUrl}</Text>}
        </Stack>
      </Paper>

      <Paper withBorder p="md" radius="sm">
        <Title order={2} size="h3">Ownership</Title>
        <Text mt="xs">Current owner: {application.data.ownerId} ({application.data.ownerType})</Text>
        {editing && !isManifestBacked && canAdmin && (
          <form onSubmit={submitOwnership}>
            <Group align="end" mt="md">
              <TextInput label="New owner ID" value={ownerId} onChange={(event) => setOwnerId(event.currentTarget.value)} required />
              <NativeSelect
                label="New owner type"
                value={ownerType}
                onChange={(event) => setOwnerType(event.currentTarget.value as PrincipalType)}
                data={['User', 'Group']}
              />
              <Button type="submit" loading={transferOwnership.isPending}>Transfer Ownership</Button>
            </Group>
          </form>
        )}
      </Paper>

      <Paper withBorder p="md" radius="sm">
        <Title order={2} size="h3">Maintainers</Title>
        {editing && !isManifestBacked && canAdmin && (
          <form onSubmit={submitMaintainer}>
            <Group align="end" mt="md">
              <TextInput label="New maintainer ID" value={maintainerId} onChange={(event) => setMaintainerId(event.currentTarget.value)} required />
              <NativeSelect
                label="New maintainer type"
                value={maintainerType}
                onChange={(event) => setMaintainerType(event.currentTarget.value as PrincipalType)}
                data={['User', 'Group']}
              />
              <Button type="submit" loading={maintainers.add.isPending}>Add Maintainer</Button>
            </Group>
          </form>
        )}
        <Stack gap="xs" mt="md">
          {application.data.maintainers.length === 0 && <Text c="dimmed">No maintainers assigned</Text>}
          {application.data.maintainers.map((maintainer) => (
            <Group key={maintainer.id} justify="space-between">
              <Text>{maintainer.principalId} ({maintainer.principalType})</Text>
              {editing && !isManifestBacked && canAdmin && (
                <Button
                  size="xs"
                  variant="subtle"
                  color="red"
                  onClick={() => maintainers.remove.mutate({ principalId: maintainer.principalId, concurrencyToken })}
                >
                  Remove
                </Button>
              )}
            </Group>
          ))}
        </Stack>
      </Paper>

      <Paper withBorder p="md" radius="sm">
        <Group justify="space-between" align="center">
          <Title order={2} size="h3">Permissions</Title>
          {editing && !isManifestBacked && canWrite && (
            <Button variant="light" onClick={() => void toggleLifecycle()} loading={lifecycle.isPending}>
              {application.data.status === 'Active' ? 'Disable' : 'Enable'}
            </Button>
          )}
        </Group>
        {editing && !isManifestBacked && canWrite && (
          <form onSubmit={submitPermission}>
            <Group align="end" mt="md">
              <TextInput label="Permission name" value={permissionName} onChange={(event) => setPermissionName(event.currentTarget.value)} required />
              <TextInput label="Permission category" value={permissionCategory} onChange={(event) => setPermissionCategory(event.currentTarget.value)} />
              <Button type="submit" loading={addPermission.isPending}>Add Permission</Button>
            </Group>
            <Textarea label="Permission description" mt="sm" value={permissionDescription} onChange={(event) => setPermissionDescription(event.currentTarget.value)} />
          </form>
        )}
        <Stack gap="xs" mt="md">
          {application.data.permissions.map((permission) => (
            <Paper key={permission.id} withBorder p="sm" radius="sm">
              <Text fw={500}>{permission.fullPermissionKey}</Text>
              {permission.description && <Text size="sm" c="dimmed">{permission.description}</Text>}
              {permission.category && <Badge mt="xs" variant="light">{permission.category}</Badge>}
            </Paper>
          ))}
        </Stack>
      </Paper>

      <Paper withBorder p="md" radius="sm">
        <Title order={2} size="h3">Catalog</Title>
        {catalog.isError && <Alert color="red">{getApiErrorMessage(catalog.error)}</Alert>}
        <Stack gap="xs" mt="md">
          {(catalog.data?.items ?? []).map((item) => (
            <Text key={item.fullPermissionKey}>{item.fullPermissionKey}</Text>
          ))}
          {!catalog.isLoading && (catalog.data?.items.length ?? 0) === 0 && <Text c="dimmed">No catalog permissions found</Text>}
        </Stack>
      </Paper>

      <Paper withBorder p="md" radius="sm">
        <Title order={2} size="h3">History</Title>
        {history.isError && <Alert color="red">{getApiErrorMessage(history.error)}</Alert>}
        <Stack gap="xs" mt="md">
          {(history.data?.removedPermissions ?? []).map((permission) => (
            <Text key={permission.fullPermissionKey}>{permission.fullPermissionKey}</Text>
          ))}
          {!history.isLoading && (history.data?.removedPermissions.length ?? 0) === 0 && <Text c="dimmed">No removed permissions</Text>}
        </Stack>
      </Paper>

      <Paper withBorder p="md" radius="sm">
        <Title order={2} size="h3">Diagnostics</Title>
        {diagnostics.isError && <Alert color="red">{getApiErrorMessage(diagnostics.error)}</Alert>}
        <Stack gap="xs" mt="md">
          {(diagnostics.data?.issues ?? []).map((issue) => (
            <Group key={`${issue.fullPermissionKey}-${issue.roleName ?? ''}`}>
              <Text>{issue.fullPermissionKey}</Text>
              {issue.roleName && <Badge variant="light">{issue.roleName}</Badge>}
              {issue.issueType && <Badge color="red" variant="light">{issue.issueType}</Badge>}
            </Group>
          ))}
          {!diagnostics.isLoading && (diagnostics.data?.issues.length ?? 0) === 0 && <Text c="dimmed">No permission diagnostics found</Text>}
        </Stack>
      </Paper>
    </Stack>
  );
}

function ApplicationPermissionStatusBadge({ status }: { status: string }) {
  const color = status === 'Active' ? 'green' : status === 'Disabled' ? 'yellow' : 'gray';
  return <Badge color={color} variant="light">{status}</Badge>;
}
