import {
  Alert,
  Badge,
  Button,
  Combobox,
  Group,
  NativeSelect,
  Paper,
  Stack,
  Tabs,
  Text,
  Textarea,
  TextInput,
  Title,
  useCombobox,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useQuery } from '@tanstack/react-query';
import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router';
import { BackLink } from '@/components/DetailPrimitives';
import { EntityActionGroup } from '@/components/EntityActionMenu';
import { FoundationTable, type FoundationColumn } from '@/components/FoundationTable';
import { PageHeader, PageToolbar } from '@/components/PagePrimitives';
import { getGroup, getGroups, type GroupListItem } from '@/features/groups/groups-api';
import { getUser, getUsers, type UserListItem } from '@/features/users/users-api';
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
  useUpdateRegisteredApplication,
} from './application-permissions-hooks';
import type {
  PermissionManifestPermission,
  PermissionManifestRequest,
  PrincipalType,
  RegisteredApplicationListItem,
} from './application-permissions-api';

const pageSize = 20;
const applicationIdentifierPattern = /^[a-zA-Z0-9][a-zA-Z0-9._:-]*$/;
const permissionKeyPattern = /^[a-zA-Z0-9][a-zA-Z0-9._:-]*$/;

type ApplicationPermissionsPageProps = {
  permissions?: string[];
};

type PrincipalOption = {
  id: string;
  type: PrincipalType;
  label: string;
  detail: string;
};

type PermissionManifestFormValues = {
  endpoint: string;
  applicationId: string;
  displayName: string;
  description: string;
  version: string;
  ownerId: string;
  ownerType: 'user' | 'group';
  permissions: PermissionManifestPermission[];
};

type PrincipalFormValues = {
  principal: PrincipalOption | null;
};

type PermissionFormValues = {
  permissionName: string;
  permissionCategory: string;
  permissionDescription: string;
};

function required(value: string, message: string): string | null {
  return value.trim() ? null : message;
}

function maxLength(value: string, max: number, label: string): string | null {
  return value.length <= max ? null : `${label} must be ${max} characters or fewer.`;
}

function validUrl(value: string, message: string): string | null {
  try {
    const url = new URL(value.trim());
    return url.protocol === 'https:' || url.protocol === 'http:' ? null : message;
  } catch {
    return message;
  }
}

function validIdentifier(value: string, message: string): string | null {
  return applicationIdentifierPattern.test(value.trim()) ? null : message;
}

function validPermissionKey(value: string): string | null {
  return permissionKeyPattern.test(value.trim())
    ? null
    : 'Permission keys can contain letters, numbers, dots, underscores, colons, and hyphens.';
}

function firstError(...errors: Array<string | null>): string | null {
  return errors.find((error): error is string => error !== null) ?? null;
}

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
      align: 'right',
      cell: (application) => (
        <EntityActionGroup
          actions={[{ label: `View ${application.displayName}`, onClick: () => navigate(`/application-permissions/${application.id}`) }]}
        />
      ),
    },
  ];

  return (
    <Stack gap="lg">
      <PageHeader
        title="Permissions"
        description="Manage application-owned permission registries and assignments."
        actions={canWrite && <Button onClick={() => navigate('/application-permissions/new')}>Add Application</Button>}
      />

      {!canWrite && <Alert color="blue">Read-only access. Application permission changes require additional permissions.</Alert>}

      <PageToolbar
        searchLabel="Search applications"
        searchPlaceholder="Search applications..."
        searchValue={search}
        resultCount={applications.data?.totalCount}
        onSearchChange={setSearch}
        onClear={() => {
          setSearch('');
          setSubmittedSearch('');
          setPage(1);
        }}
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
      <BackLink label="Back to Permissions" to="/application-permissions" />
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
  const form = useForm<PermissionManifestFormValues>({
    mode: 'controlled',
    validateInputOnBlur: true,
    initialValues: {
      endpoint: '',
      applicationId: '',
      displayName: '',
      description: '',
      version: '',
      ownerId: '',
      ownerType: 'user',
      permissions: [{ key: '', displayName: '', description: '', category: '' }],
    },
    validate: (values) => {
      const keys = values.permissions.map((permission) => permission.key.trim()).filter(Boolean);
      const errors: Record<string, string | null> = {
        applicationId: firstError(
          required(values.applicationId, 'Application ID is required.'),
          validIdentifier(values.applicationId, 'Application ID can contain letters, numbers, dots, underscores, colons, and hyphens.'),
          maxLength(values.applicationId, 128, 'Application ID')
        ),
        displayName: firstError(
          required(values.displayName, 'Display name is required.'),
          maxLength(values.displayName, 200, 'Display name')
        ),
        description: maxLength(values.description, 1000, 'Description'),
        version: firstError(
          required(values.version, 'Version is required.'),
          maxLength(values.version, 64, 'Version')
        ),
        ownerId: firstError(
          required(values.ownerId, 'Owner ID is required.'),
          maxLength(values.ownerId, 256, 'Owner ID')
        ),
        permissions: new Set(keys).size === keys.length ? null : 'Permission keys must be unique.',
      };

      values.permissions.forEach((permission, index) => {
        errors[`permissions.${index}.key`] = firstError(
          required(permission.key, `Permission key ${index + 1} is required.`),
          validPermissionKey(permission.key),
          maxLength(permission.key, 200, `Permission key ${index + 1}`)
        );
        errors[`permissions.${index}.displayName`] = firstError(
          required(permission.displayName, `Permission display name ${index + 1} is required.`),
          maxLength(permission.displayName, 200, `Permission display name ${index + 1}`)
        );
        errors[`permissions.${index}.category`] = maxLength(permission.category ?? '', 100, `Permission category ${index + 1}`);
        errors[`permissions.${index}.description`] = maxLength(permission.description ?? '', 1000, `Permission description ${index + 1}`);
      });

      return errors;
    },
  });

  async function submitManifest(values: PermissionManifestFormValues) {
    await onSubmit({
      manifest: {
        schemaVersion: '1.0.0',
        application: {
          id: values.applicationId.trim(),
          displayName: values.displayName.trim(),
          description: values.description.trim() || null,
          version: values.version.trim(),
        },
        permissions: values.permissions.map((permission) => ({
          key: permission.key.trim(),
          displayName: permission.displayName.trim(),
          description: permission.description?.trim() || null,
          category: permission.category?.trim() || null,
        })),
      },
      ownerId: values.ownerId.trim(),
      ownerType: values.ownerType,
      manifestBaseUrl: null,
    });
  }

  return (
    <Stack gap="lg">
      {error ? <Alert color="red">{getApiErrorMessage(error)}</Alert> : null}
      <Paper withBorder p="md" radius="sm">
        <form
          noValidate
          onSubmit={(event) => {
            event.preventDefault();
            const endpoint = form.values.endpoint.trim();
            const endpointError = firstError(
              required(endpoint, 'Well-known permissions endpoint is required.'),
              validUrl(endpoint, 'Enter a valid HTTP or HTTPS endpoint.')
            );

            if (endpointError) {
              form.setFieldError('endpoint', endpointError);
              return;
            }

            form.clearFieldError('endpoint');
            void onImportEndpoint(endpoint);
          }}
        >
          <Group align="end">
            <TextInput
              label="Well-known permissions endpoint"
              placeholder="https://patient.example/.well-known/permissions"
              type="url"
              style={{ flex: 1 }}
              {...form.getInputProps('endpoint')}
            />
            <Button type="submit" variant="light" loading={importing}>
              Import Endpoint
            </Button>
          </Group>
        </form>
      </Paper>

      <form noValidate onSubmit={form.onSubmit((values) => void submitManifest(values))}>
        <Stack gap="lg">
          <Paper withBorder p="md" radius="sm">
            <Title order={2} size="h3">Application</Title>
            <Group grow align="flex-start" mt="md">
              <TextInput aria-label="Application ID" label="Application ID" required {...form.getInputProps('applicationId')} />
              <TextInput aria-label="Display Name" label="Display Name" required {...form.getInputProps('displayName')} />
              <TextInput aria-label="Version" label="Version" required {...form.getInputProps('version')} />
            </Group>
            <Textarea aria-label="Description" label="Description" mt="md" {...form.getInputProps('description')} />
            <Group grow align="flex-start" mt="md">
              <TextInput aria-label="Owner ID" label="Owner ID" required {...form.getInputProps('ownerId')} />
              <NativeSelect
                label="Owner Type"
                data={[
                  { value: 'user', label: 'User' },
                  { value: 'group', label: 'Group' },
                ]}
                {...form.getInputProps('ownerType')}
              />
            </Group>
          </Paper>

          <Paper withBorder p="md" radius="sm">
            <Group justify="space-between" align="center">
              <Title order={2} size="h3">Permissions</Title>
              <Button
                type="button"
                variant="light"
                onClick={() => form.insertListItem('permissions', { key: '', displayName: '', description: '', category: '' })}
              >
                Add Permission
              </Button>
            </Group>
            <Stack gap="md" mt="md">
              {form.values.permissions.map((permission, index) => (
                <Paper key={index} withBorder p="sm" radius="sm">
                  <Group grow align="flex-start">
                    <TextInput aria-label={`Permission key ${index + 1}`} label={`Permission key ${index + 1}`} required {...form.getInputProps(`permissions.${index}.key`)} />
                    <TextInput aria-label={`Permission display name ${index + 1}`} label={`Permission display name ${index + 1}`} required {...form.getInputProps(`permissions.${index}.displayName`)} />
                    <TextInput aria-label={`Permission category ${index + 1}`} label={`Permission category ${index + 1}`} {...form.getInputProps(`permissions.${index}.category`)} />
                  </Group>
                  <Textarea aria-label={`Permission description ${index + 1}`} label={`Permission description ${index + 1}`} mt="sm" {...form.getInputProps(`permissions.${index}.description`)} />
                  <Button
                    type="button"
                    variant="subtle"
                    color="red"
                    mt="sm"
                    disabled={form.values.permissions.length === 1}
                    onClick={() => form.removeListItem('permissions', index)}
                  >
                    Remove
                  </Button>
                </Paper>
              ))}
            </Stack>
            {form.errors.permissions && <Alert color="red" mt="md">{form.errors.permissions}</Alert>}
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
  const updateApplication = useUpdateRegisteredApplication(applicationId);
  const addPermission = useAddApplicationPermission(applicationId);
  const maintainers = useMaintainerMutations(applicationId);
  const [editing, setEditing] = useState(false);
  const [ownerSearch, setOwnerSearch] = useState('');
  const [maintainerSearch, setMaintainerSearch] = useState('');
  const ownerForm = useForm<PrincipalFormValues>({
    mode: 'controlled',
    validateInputOnBlur: true,
    initialValues: { principal: null },
    validate: { principal: (value) => (value ? null : 'Select an owner.') },
  });
  const maintainerForm = useForm<PrincipalFormValues>({
    mode: 'controlled',
    validateInputOnBlur: true,
    initialValues: { principal: null },
    validate: { principal: (value) => (value ? null : 'Select a maintainer.') },
  });
  const manifestBaseUrlForm = useForm({
    mode: 'controlled',
    validateInputOnBlur: true,
    initialValues: { manifestBaseUrl: '' },
    validate: {
      manifestBaseUrl: (value) => firstError(
        required(value, 'Manifest Base URL is required.'),
        validUrl(value, 'Enter a valid HTTP or HTTPS manifest base URL.')
      ),
    },
  });
  const permissionForm = useForm<PermissionFormValues>({
    mode: 'controlled',
    validateInputOnBlur: true,
    initialValues: {
      permissionName: '',
      permissionCategory: '',
      permissionDescription: '',
    },
    validate: {
      permissionName: (value) => firstError(
        required(value, 'Permission name is required.'),
        validPermissionKey(value),
        maxLength(value, 200, 'Permission name')
      ),
      permissionCategory: (value) => maxLength(value, 100, 'Permission category'),
      permissionDescription: (value) => maxLength(value, 1000, 'Permission description'),
    },
  });
  const canWrite = hasPermission(permissions, 'application-permissions:write');
  const canAdmin = hasPermission(permissions, 'application-permissions:admin') || canWrite;
  const registeredApplicationData = application.data;
  const isManifestBacked = Boolean(registeredApplicationData?.manifestBaseUrl);
  const ownerOptions = usePrincipalOptions(ownerSearch, editing && !isManifestBacked && canAdmin);
  const maintainerOptions = usePrincipalOptions(maintainerSearch, editing && !isManifestBacked && canAdmin);

  if (application.isLoading) {
    return <Text c="dimmed">Loading application permission registry</Text>;
  }

  if (application.isError) {
    return <Alert color="red">{getApiErrorMessage(application.error)}</Alert>;
  }

  if (!application.data) {
    return <Alert color="red">Application permission registry not found.</Alert>;
  }

  const registeredApplication = application.data;
  const concurrencyToken = registeredApplication.concurrencyToken;

  async function toggleLifecycle() {
    await lifecycle.mutateAsync({
      status: registeredApplication.status === 'Active' ? 'Disabled' : 'Active',
      acknowledgeDependencies: true,
      concurrencyToken,
    });
  }

  async function submitOwnership(values: PrincipalFormValues) {
    if (!values.principal) {
      return;
    }

    await transferOwnership.mutateAsync({ ownerId: values.principal.id, ownerType: values.principal.type, concurrencyToken });
    ownerForm.reset();
    setOwnerSearch('');
  }

  async function submitMaintainer(values: PrincipalFormValues) {
    if (!values.principal) {
      return;
    }

    await maintainers.add.mutateAsync({ principalId: values.principal.id, principalType: values.principal.type, concurrencyToken });
    maintainerForm.reset();
    setMaintainerSearch('');
  }

  async function submitManifestBaseUrl(values: { manifestBaseUrl: string }) {
    await updateApplication.mutateAsync({
      displayName: registeredApplication.displayName,
      description: registeredApplication.description,
      manifestBaseUrl: values.manifestBaseUrl.trim(),
      concurrencyToken,
    });
    setEditing(false);
  }

  async function submitPermission(values: PermissionFormValues) {
    await addPermission.mutateAsync({
      permissionKey: values.permissionName.trim(),
      displayName: values.permissionName.trim(),
      description: values.permissionDescription.trim() || null,
      category: values.permissionCategory.trim() || null,
      concurrencyToken,
    });
    permissionForm.reset();
  }

  return (
    <Stack gap="lg" role="region" aria-label="Application permission details">
      <PageHeader
        title={registeredApplication.displayName}
        description={registeredApplication.applicationIdentifier}
        badges={[{ label: registeredApplication.status, color: registeredApplication.status === 'Active' ? 'green' : 'yellow' }]}
        actions={canWrite && (
          <Button
            variant="light"
            onClick={() => {
              if (!editing) {
                manifestBaseUrlForm.setValues({ manifestBaseUrl: registeredApplication.manifestBaseUrl ?? '' });
              }

              setEditing((value) => !value);
            }}
          >
            {editing ? 'Done' : 'Edit'}
          </Button>
        )}
      />

      <Tabs defaultValue="overview" keepMounted={false}>
        <Tabs.List>
          <Tabs.Tab value="overview">Overview</Tabs.Tab>
          <Tabs.Tab value="permissions">Permissions</Tabs.Tab>
          <Tabs.Tab value="maintainers">Maintainers</Tabs.Tab>
          <Tabs.Tab value="history">History</Tabs.Tab>
          <Tabs.Tab value="diagnostics">Diagnostics</Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="overview" pt="md">
          <Stack gap="md">
            <Paper withBorder p="md" radius="sm">
              <Stack gap="xs">
                <Text size="sm">Description: {registeredApplication.description || 'Not set'}</Text>
                <Text size="sm">Manifest Version: {registeredApplication.manifestVersion || 'Not set'}</Text>
                <Text size="sm">Schema Version: {registeredApplication.schemaVersion || 'Not set'}</Text>
                {registeredApplication.manifestBaseUrl && <Text size="sm">Manifest Base URL: {registeredApplication.manifestBaseUrl}</Text>}
              </Stack>
            </Paper>

            {editing && isManifestBacked && canWrite && (
              <Paper withBorder p="md" radius="sm">
                <form noValidate onSubmit={manifestBaseUrlForm.onSubmit((values) => void submitManifestBaseUrl(values))}>
                  <Group align="end">
                    <TextInput
                      label="Manifest Base URL"
                      required
                      style={{ flex: 1 }}
                      type="url"
                      {...manifestBaseUrlForm.getInputProps('manifestBaseUrl')}
                    />
                    <Button type="submit" loading={updateApplication.isPending}>Save URL</Button>
                  </Group>
                </form>
              </Paper>
            )}

            <Paper withBorder p="md" radius="sm">
              <Title order={2} size="h3">Ownership</Title>
              <Group gap="xs" mt="xs">
                <Text>Current owner:</Text>
                <PrincipalDisplay principalId={registeredApplication.ownerId} principalType={registeredApplication.ownerType} />
              </Group>
              {editing && !isManifestBacked && canAdmin && (
                <form noValidate onSubmit={ownerForm.onSubmit((values) => void submitOwnership(values))}>
                  <Group align="end" mt="md">
                    <PrincipalSearchInput
                      label="New owner"
                      options={ownerOptions}
                      query={ownerSearch}
                      {...ownerForm.getInputProps('principal')}
                      onQueryChange={(query) => {
                        setOwnerSearch(query);
                        ownerForm.setFieldValue('principal', null);
                      }}
                      onSelect={(option) => {
                        ownerForm.setFieldValue('principal', option);
                        setOwnerSearch(option.label);
                      }}
                    />
                    <Button type="submit" loading={transferOwnership.isPending}>Transfer Ownership</Button>
                  </Group>
                </form>
              )}
            </Paper>
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="permissions" pt="md">
          <Stack gap="md">
            <Paper withBorder p="md" radius="sm">
              <Group justify="space-between" align="center">
                <Title order={2} size="h3">Permissions</Title>
                {editing && !isManifestBacked && canWrite && (
                  <Button variant="light" onClick={() => void toggleLifecycle()} loading={lifecycle.isPending}>
                    {registeredApplication.status === 'Active' ? 'Disable' : 'Enable'}
                  </Button>
                )}
              </Group>
              {editing && !isManifestBacked && canWrite && (
                <form noValidate onSubmit={permissionForm.onSubmit((values) => void submitPermission(values))}>
                  <Group align="end" mt="md">
                    <TextInput label="Permission name" required {...permissionForm.getInputProps('permissionName')} />
                    <TextInput label="Permission category" {...permissionForm.getInputProps('permissionCategory')} />
                    <Button type="submit" loading={addPermission.isPending}>Add Permission</Button>
                  </Group>
                  <Textarea label="Permission description" mt="sm" {...permissionForm.getInputProps('permissionDescription')} />
                </form>
              )}
              <Stack gap="xs" mt="md">
                {registeredApplication.permissions.map((permission) => (
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
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="maintainers" pt="md">
          <Paper withBorder p="md" radius="sm">
            <Title order={2} size="h3">Maintainers</Title>
            {editing && !isManifestBacked && canAdmin && (
              <form noValidate onSubmit={maintainerForm.onSubmit((values) => void submitMaintainer(values))}>
                <Group align="end" mt="md">
                  <PrincipalSearchInput
                    label="New maintainer"
                    options={maintainerOptions}
                    query={maintainerSearch}
                    {...maintainerForm.getInputProps('principal')}
                    onQueryChange={(query) => {
                      setMaintainerSearch(query);
                      maintainerForm.setFieldValue('principal', null);
                    }}
                    onSelect={(option) => {
                      maintainerForm.setFieldValue('principal', option);
                      setMaintainerSearch(option.label);
                    }}
                  />
                  <Button type="submit" loading={maintainers.add.isPending}>Add Maintainer</Button>
                </Group>
              </form>
            )}
            <Stack gap="xs" mt="md">
              {registeredApplication.maintainers.length === 0 && <Text c="dimmed">No maintainers assigned</Text>}
              {registeredApplication.maintainers.map((maintainer) => (
                <Group key={maintainer.id} justify="space-between">
                  <PrincipalDisplay principalId={maintainer.principalId} principalType={maintainer.principalType} />
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
        </Tabs.Panel>

        <Tabs.Panel value="history" pt="md">
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
        </Tabs.Panel>

        <Tabs.Panel value="diagnostics" pt="md">
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
        </Tabs.Panel>
      </Tabs>
    </Stack>
  );
}

function ApplicationPermissionStatusBadge({ status }: { status: string }) {
  const color = status === 'Active' ? 'green' : status === 'Disabled' ? 'yellow' : 'gray';
  return <Badge color={color} variant="light">{status}</Badge>;
}

function usePrincipalOptions(query: string, enabled: boolean): PrincipalOption[] {
  const users = useQuery({
    queryKey: ['application-permissions', 'principal-users', query],
    queryFn: () => getUsers({ page: 1, pageSize: 20, search: query || undefined }),
    enabled,
    staleTime: 30_000,
  });
  const groups = useQuery({
    queryKey: ['application-permissions', 'principal-groups', query],
    queryFn: () => getGroups({ page: 1, pageSize: 20, search: query || undefined }),
    enabled,
    staleTime: 30_000,
  });

  return [
    ...(users.data?.items.map(mapUserOption) ?? []),
    ...(groups.data?.items.map(mapGroupOption) ?? []),
  ];
}

function PrincipalSearchInput({
  label,
  options,
  query,
  value,
  error,
  onBlur,
  onChange,
  onFocus,
  onQueryChange,
  onSelect,
}: {
  label: string;
  options: PrincipalOption[];
  query: string;
  value?: PrincipalOption | null;
  error?: ReactNode;
  onBlur?: () => void;
  onChange?: (value: PrincipalOption | null) => void;
  onFocus?: () => void;
  onQueryChange: (query: string) => void;
  onSelect: (option: PrincipalOption) => void;
}) {
  const combobox = useCombobox({
    onDropdownClose: () => combobox.resetSelectedOption(),
    onDropdownOpen: () => combobox.selectFirstOption(),
  });

  const optionNodes = options.slice(0, 10).map((option) => (
    <Combobox.Option
      active={value?.id === option.id && value.type === option.type}
      aria-label={option.label}
      key={`${option.type}-${option.id}`}
      value={`${option.type}:${option.id}`}
    >
      <Group justify="space-between" gap="md" wrap="nowrap">
        <Stack gap={0}>
          <Text fw={500} size="sm">{option.label}</Text>
          <Text c="dimmed" size="xs">{option.detail}</Text>
        </Stack>
        <Badge variant="light">{option.type}</Badge>
      </Group>
    </Combobox.Option>
  ));

  useEffect(() => {
    if (query.trim().length > 0 && options.length > 0) {
      combobox.openDropdown();
      combobox.updateSelectedOptionIndex();
    }
  }, [combobox, options.length, query]);

  return (
    <Combobox
      hideDetached={false}
      onOptionSubmit={(submittedValue) => {
        const option = options.find((item) => `${item.type}:${item.id}` === submittedValue);
        if (!option) {
          return;
        }

        onChange?.(option);
        onSelect(option);
        combobox.closeDropdown();
      }}
      store={combobox}
      withinPortal={false}
    >
      <Combobox.Target>
        <TextInput
          aria-expanded={combobox.dropdownOpened}
          error={error}
          label={label}
          onBlur={() => {
            combobox.closeDropdown();
            onBlur?.();
          }}
          onChange={(event) => {
            onQueryChange(event.currentTarget.value);
            onChange?.(null);
            combobox.openDropdown();
            combobox.updateSelectedOptionIndex();
          }}
          onClick={() => combobox.openDropdown()}
          onFocus={() => {
            combobox.openDropdown();
            onFocus?.();
          }}
          placeholder="Search users or groups..."
          rightSection={<Combobox.Chevron />}
          rightSectionPointerEvents="none"
          role="combobox"
          style={{ flex: 1, minWidth: 260 }}
          value={query}
        />
      </Combobox.Target>

      <Combobox.Dropdown>
        <Combobox.Options>
          {optionNodes.length > 0 ? optionNodes : <Combobox.Empty>No users or groups found</Combobox.Empty>}
        </Combobox.Options>
      </Combobox.Dropdown>
    </Combobox>
  );
}

function PrincipalDisplay({ principalId, principalType }: { principalId: string; principalType: PrincipalType }) {
  const user = useQuery({
    queryKey: ['application-permissions', 'principal-user', principalId],
    queryFn: () => getUser(principalId),
    enabled: principalType === 'User' && principalId.length > 0,
    staleTime: 30_000,
    retry: false,
  });
  const group = useQuery({
    queryKey: ['application-permissions', 'principal-group', principalId],
    queryFn: () => getGroup(principalId),
    enabled: principalType === 'Group' && principalId.length > 0,
    staleTime: 30_000,
    retry: false,
  });
  const label = principalType === 'Group'
    ? group.data?.name
    : user.data?.displayName || user.data?.email;
  const detail = principalType === 'Group'
    ? group.data?.description
    : user.data?.email;

  return (
    <Stack gap={0}>
      <Text fw={500} size="sm">{label ?? principalId}</Text>
      <Text c="dimmed" size="xs">{detail ? `${detail} ` : null}{principalType}</Text>
    </Stack>
  );
}

function mapUserOption(user: UserListItem): PrincipalOption {
  return {
    id: user.id,
    type: 'User',
    label: user.displayName || user.email,
    detail: user.email,
  };
}

function mapGroupOption(group: GroupListItem): PrincipalOption {
  return {
    id: group.id,
    type: 'Group',
    label: group.name,
    detail: group.description || group.id,
  };
}
