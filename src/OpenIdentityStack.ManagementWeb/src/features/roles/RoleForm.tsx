import { Alert, Button, Checkbox, Group, Stack, Textarea, TextInput } from '@mantine/core';
import { FormEvent, useState } from 'react';
import { getApiErrorMessage } from '@/lib/admin-api';
import { PermissionSelector } from './PermissionSelector';
import type { CreateRoleRequest, Role, UpdateRoleRequest } from './roles-api';

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
  loading?: boolean;
  onSubmit: (data: CreateRoleRequest | UpdateRoleRequest) => Promise<void> | void;
  onCancel: () => void;
};

export function RoleForm({ role, mode, error, loading = false, onSubmit, onCancel }: RoleFormProps) {
  const [values, setValues] = useState<RoleFormValues>({
    name: role?.name ?? '',
    displayName: role?.displayName ?? '',
    description: role?.description ?? '',
    permissions: role?.permissions ?? [],
    acknowledgeWildcardGrant: false,
  });
  const [validationError, setValidationError] = useState<string | null>(null);
  const wildcardSelected = values.permissions.some(isWildcardPermission);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const errorMessage = validate(values, mode, wildcardSelected);
    if (errorMessage) {
      setValidationError(errorMessage);
      return;
    }

    setValidationError(null);

    const common = {
      displayName: values.displayName.trim(),
      description: values.description.trim() || null,
      permissions: values.permissions,
      acknowledgeWildcardGrant: wildcardSelected ? values.acknowledgeWildcardGrant : false,
    };

    if (mode === 'create') {
      await onSubmit({
        name: values.name.trim(),
        ...common,
      });
      return;
    }

    await onSubmit(common);
  }

  return (
    <form onSubmit={(event) => void handleSubmit(event)}>
      <Stack gap="md">
        {(validationError || error) && (
          <Alert color="red">
            {validationError ?? getApiErrorMessage(error)}
          </Alert>
        )}

        <TextInput
          label="Name"
          aria-label="Name"
          value={values.name}
          disabled={mode === 'edit'}
          onChange={(event) => setValues({ ...values, name: event.currentTarget.value })}
          required
        />
        <TextInput
          label="Display name"
          aria-label="Display name"
          value={values.displayName}
          onChange={(event) => setValues({ ...values, displayName: event.currentTarget.value })}
          required
        />
        <Textarea
          label="Description"
          value={values.description}
          onChange={(event) => setValues({ ...values, description: event.currentTarget.value })}
          autosize
          minRows={3}
        />

        <PermissionSelector
          selectedPermissions={values.permissions}
          onChange={(permissions) => setValues({ ...values, permissions })}
        />

        {wildcardSelected && (
          <Checkbox
            label="Acknowledge wildcard grant"
            checked={values.acknowledgeWildcardGrant}
            onChange={(event) => setValues({ ...values, acknowledgeWildcardGrant: event.currentTarget.checked })}
          />
        )}

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
  );
}

function validate(values: RoleFormValues, mode: 'create' | 'edit', wildcardSelected: boolean) {
  if (mode === 'create' && !/^[a-z0-9-]{3,50}$/.test(values.name.trim())) {
    return 'Role name must be 3-50 lowercase letters, numbers, or hyphens.';
  }

  if (values.displayName.trim().length < 3 || values.displayName.trim().length > 100) {
    return 'Display name must be 3-100 characters.';
  }

  if (values.description.length > 500) {
    return 'Description must be 500 characters or fewer.';
  }

  if (values.permissions.length === 0) {
    return 'Select at least one permission.';
  }

  if (wildcardSelected && !values.acknowledgeWildcardGrant) {
    return 'Acknowledge wildcard grant before saving this role.';
  }

  return null;
}

function isWildcardPermission(permission: string) {
  return permission === '*' || permission.endsWith(':*');
}
