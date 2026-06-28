import {
  ActionIcon,
  Alert,
  Box,
  Button,
  Group,
  Modal,
  SegmentedControl,
  Select,
  Stack,
  Text,
  TextInput,
} from '@mantine/core';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import type { OwnerType, PermissionManifestPermission } from '@openidentitystack/admin-api-client';
import { Icon } from '@/components/Icon';
import { api, getApiErrorMessage } from '@/lib/api';

type DraftPermission = { key: string; displayName: string; category: string };

const emptyPermission = (): DraftPermission => ({ key: '', displayName: '', category: '' });

export function RegisterManifestModal({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [mode, setMode] = useState<'import' | 'manual'>('import');

  const [endpoint, setEndpoint] = useState('');
  const [appId, setAppId] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [version, setVersion] = useState('1.0.0');
  const [ownerType, setOwnerType] = useState<OwnerType>('group');
  const [ownerId, setOwnerId] = useState('');
  const [permissions, setPermissions] = useState<DraftPermission[]>([emptyPermission()]);
  const [error, setError] = useState<string | null>(null);

  const finish = (id: string) => {
    void queryClient.invalidateQueries({ queryKey: ['application-permissions'] });
    onClose();
    if (id) {
      navigate(`/application-permissions/${id}`);
    }
  };

  const importMutation = useMutation({
    mutationFn: () => api.applicationPermissions.importPermissionManifestFromEndpoint(endpoint.trim()),
    onSuccess: (response) => finish(response.id),
    onError: (cause) => setError(getApiErrorMessage(cause)),
  });

  const registerMutation = useMutation({
    mutationFn: () => {
      const manifestPermissions: PermissionManifestPermission[] = permissions
        .filter((permission) => permission.key.trim())
        .map((permission) => ({
          key: permission.key.trim(),
          displayName: permission.displayName.trim() || permission.key.trim(),
          category: permission.category.trim() || null,
        }));
      return api.applicationPermissions.registerPermissionManifest({
        manifest: {
          schemaVersion: '1.0.0',
          application: { id: appId.trim(), displayName: displayName.trim(), version: version.trim() },
          permissions: manifestPermissions,
        },
        ownerId: ownerId.trim(),
        ownerType,
      });
    },
    onSuccess: (response) => finish(response.id),
    onError: (cause) => setError(getApiErrorMessage(cause)),
  });

  const setPermissionAt = (index: number, patch: Partial<DraftPermission>) =>
    setPermissions((list) => list.map((item, i) => (i === index ? { ...item, ...patch } : item)));
  const removePermissionAt = (index: number) =>
    setPermissions((list) => (list.length <= 1 ? [emptyPermission()] : list.filter((_, i) => i !== index)));

  const submit = () => {
    setError(null);
    if (mode === 'import') {
      if (!/^https?:\/\//.test(endpoint.trim())) {
        setError('Enter a valid manifest endpoint URL.');
        return;
      }
      importMutation.mutate();
      return;
    }
    if (!appId.trim() || !displayName.trim() || !ownerId.trim()) {
      setError('Application ID, display name and owner are required.');
      return;
    }
    if (!/^[a-z][a-z0-9-]{2,62}$/.test(appId.trim())) {
      setError('Application ID must be a slug: a lowercase letter followed by lowercase letters, digits or hyphens.');
      return;
    }
    if (!permissions.some((permission) => permission.key.trim())) {
      setError('Declare at least one permission.');
      return;
    }
    registerMutation.mutate();
  };

  const pending = importMutation.isPending || registerMutation.isPending;

  return (
    <Modal opened onClose={onClose} title="Register application" centered size="lg">
      <Stack gap="md">
        {error && (
          <Alert color="red" icon={<Icon name="circle-alert" size={18} />} variant="light">
            {error}
          </Alert>
        )}

        <SegmentedControl
          fullWidth
          value={mode}
          onChange={(value) => setMode(value as 'import' | 'manual')}
          data={[
            { value: 'import', label: 'Import from endpoint' },
            { value: 'manual', label: 'Register manually' },
          ]}
        />

        {mode === 'import' ? (
          <TextInput
            label="Manifest endpoint"
            description="A well-known permissions manifest URL the server will fetch and import."
            placeholder="https://api.example.com/.well-known/permissions"
            leftSection={<Icon name="cloud-download" size={15} />}
            value={endpoint}
            onChange={(event) => setEndpoint(event.currentTarget.value)}
          />
        ) : (
          <>
            <TextInput
              label="Application ID"
              description="A short slug identifying the application (lowercase letters, digits, hyphens)."
              placeholder="orders-api"
              required
              style={{ fontFamily: 'var(--mw-mono)' }}
              value={appId}
              onChange={(event) => setAppId(event.currentTarget.value)}
            />
            <Group grow>
              <TextInput label="Display name" placeholder="Orders API" required value={displayName} onChange={(event) => setDisplayName(event.currentTarget.value)} />
              <TextInput label="Manifest version" placeholder="1.0.0" value={version} onChange={(event) => setVersion(event.currentTarget.value)} />
            </Group>
            <Group grow align="flex-end">
              <Select
                label="Owner type"
                data={[
                  { value: 'group', label: 'Group' },
                  { value: 'user', label: 'User' },
                ]}
                allowDeselect={false}
                value={String(ownerType)}
                onChange={(value) => value && setOwnerType(value as OwnerType)}
              />
              <TextInput
                label="Owner ID"
                description="The user or group that owns the manifest."
                placeholder="platform-engineering"
                required
                value={ownerId}
                onChange={(event) => setOwnerId(event.currentTarget.value)}
              />
            </Group>

            <Box>
              <Text fw={600} size="sm">
                Declared permissions
              </Text>
              <Text c="dimmed" size="xs" mb={6}>
                The scopes this application exposes for roles to grant.
              </Text>
              <Stack gap="xs">
                {permissions.map((permission, index) => (
                  <Group key={index} gap="xs" wrap="nowrap" align="flex-start">
                    <TextInput
                      aria-label={`Permission key ${index + 1}`}
                      placeholder="orders:read"
                      style={{ flex: 1.2, fontFamily: 'var(--mw-mono)' }}
                      value={permission.key}
                      onChange={(event) => setPermissionAt(index, { key: event.currentTarget.value })}
                    />
                    <TextInput
                      aria-label={`Permission name ${index + 1}`}
                      placeholder="Read orders"
                      style={{ flex: 1.4 }}
                      value={permission.displayName}
                      onChange={(event) => setPermissionAt(index, { displayName: event.currentTarget.value })}
                    />
                    <TextInput
                      aria-label={`Permission category ${index + 1}`}
                      placeholder="Category"
                      style={{ flex: 1 }}
                      value={permission.category}
                      onChange={(event) => setPermissionAt(index, { category: event.currentTarget.value })}
                    />
                    <ActionIcon
                      aria-label="Remove permission"
                      color="gray"
                      variant="default"
                      size="lg"
                      disabled={permissions.length <= 1 && !permission.key.trim()}
                      onClick={() => removePermissionAt(index)}
                    >
                      <Icon name="trash-2" size={16} />
                    </ActionIcon>
                  </Group>
                ))}
              </Stack>
              <Button
                size="xs"
                variant="subtle"
                leftSection={<Icon name="plus" size={14} />}
                mt={6}
                onClick={() => setPermissions((list) => [...list, emptyPermission()])}
              >
                Add permission
              </Button>
            </Box>
          </>
        )}

        <Group justify="flex-end" gap="sm" mt="xs">
          <Button variant="default" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={pending} onClick={submit}>
            {mode === 'import' ? 'Import manifest' : 'Register application'}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
