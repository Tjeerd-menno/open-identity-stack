import { Alert, Button, Group, Stack, Text, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useState } from 'react';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { FormSection } from '@/components/FormPrimitives';
import { getApiErrorMessage } from '@/lib/admin-api';
import { firstError, maxLength, required } from '@/lib/form-validation';
import { getEffectivePermissionCount } from './permission-count';
import { PermissionSelector } from './PermissionSelector';
import type { CreateRoleRequest, PlatformPermissionCatalogItem, Role, UpdateRoleRequest } from './roles-api';

type RoleFormValues = {
  name: string;
  displayName: string;
  description: string;
  permissions: string[];
  acknowledgeWildcardGrant: boolean;
};

type RoleFormProps = {
  role?: Role;
  mode: 'create' | 'edit';
  error?: unknown;
  catalogItems?: PlatformPermissionCatalogItem[];
  loading?: boolean;
  onSubmit: (data: CreateRoleRequest | UpdateRoleRequest) => Promise<void> | void;
  onCancel: () => void;
};

export function RoleForm({ role, mode, error, catalogItems, loading = false, onSubmit, onCancel }: RoleFormProps) {
  const [pendingWildcardSubmission, setPendingWildcardSubmission] = useState<CreateRoleRequest | UpdateRoleRequest | null>(null);
  const form = useForm<RoleFormValues>({
    mode: 'controlled',
    validateInputOnBlur: true,
    initialValues: {
      name: role?.name ?? '',
      displayName: role?.displayName ?? '',
      description: role?.description ?? '',
      permissions: role?.permissions ?? [],
      acknowledgeWildcardGrant: false,
    },
    validate: (values) => {
      return {
        name: mode === 'create' && !/^[a-z0-9-]{3,50}$/.test(values.name.trim())
          ? 'Role name must be 3-50 lowercase letters, numbers, or hyphens.'
          : null,
        displayName: firstError(
          required(values.displayName, 'Display name is required.'),
          values.displayName.trim().length < 3 ? 'Display name must be 3-100 characters.' : null,
          maxLength(values.displayName, 100, 'Display name')
        ),
        description: maxLength(values.description, 500, 'Description'),
        permissions: values.permissions.length === 0 ? 'Select at least one permission.' : null,
      };
    },
  });
  const values = form.values;
  const errorMessage = error ? getApiErrorMessage(error) : null;

  async function handleSubmit(values: RoleFormValues) {
    const wildcardSelected = values.permissions.some(isWildcardPermission);
    const common = {
      displayName: values.displayName.trim(),
      description: values.description.trim() || null,
      permissions: values.permissions,
      acknowledgeWildcardGrant: wildcardSelected,
    };

    const submission = mode === 'create'
      ? {
        name: values.name.trim(),
        ...common,
      }
      : common;

    if (wildcardSelected) {
      setPendingWildcardSubmission(submission);
      return;
    }

    await onSubmit(submission);
  }

  async function handleConfirmWildcardSubmission() {
    if (!pendingWildcardSubmission) {
      return;
    }

    await onSubmit(pendingWildcardSubmission);
    setPendingWildcardSubmission(null);
  }

  return (
    <>
      <form noValidate onSubmit={form.onSubmit((values) => void handleSubmit(values))}>
        <Stack gap="md">
          {errorMessage && <Alert color="red">{errorMessage}</Alert>}

          <FormSection title="Role details">
            <TextInput
              label="Name"
              aria-label="Name"
              disabled={mode === 'edit'}
              required
              {...form.getInputProps('name')}
            />
            <TextInput
              label="Display name"
              aria-label="Display name"
              required
              {...form.getInputProps('displayName')}
            />
            <Textarea
              label="Description"
              autosize
              minRows={3}
              {...form.getInputProps('description')}
            />
          </FormSection>

          <FormSection title="Permissions">
            <PermissionSelector
              selectedPermissions={values.permissions}
              onChange={(permissions) => form.setFieldValue('permissions', permissions)}
            />
            <Text size="sm" c="dimmed">
              Permission count: {getEffectivePermissionCount(values.permissions, catalogItems)}
            </Text>
            {form.errors.permissions && <Alert color="red">{form.errors.permissions}</Alert>}
          </FormSection>

          <Group justify="flex-end">
            <Button type="button" variant="default" onClick={onCancel}>
              Cancel
            </Button>
            <Button type="submit" loading={loading}>
              {mode === 'create' ? 'Create role' : 'Save role'}
            </Button>
          </Group>
        </Stack>
      </form>

      <ConfirmDialog
        opened={pendingWildcardSubmission !== null}
        onOpenChange={(opened) => {
          if (!opened) {
            setPendingWildcardSubmission(null);
          }
        }}
        title="Confirm wildcard permissions"
        message="The permissions include wildcard permissions. Are you sure you want to save this role?"
        confirmLabel="Save role"
        confirmColor="blue"
        loading={loading}
        onConfirm={handleConfirmWildcardSubmission}
      />
    </>
  );
}

function isWildcardPermission(permission: string) {
  return permission === '*' || permission.endsWith(':*');
}
