import { Alert, Button, Checkbox, Group, Stack, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { getApiErrorMessage } from '@/lib/admin-api';
import { firstError, maxLength, required } from '@/lib/form-validation';
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
      const wildcardSelected = values.permissions.some(isWildcardPermission);
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
        acknowledgeWildcardGrant: wildcardSelected && !values.acknowledgeWildcardGrant
          ? 'Acknowledge wildcard grant before saving this role.'
          : null,
      };
    },
  });
  const values = form.values;
  const wildcardSelected = values.permissions.some(isWildcardPermission);
  const errorMessage = error ? getApiErrorMessage(error) : null;

  async function handleSubmit(values: RoleFormValues) {
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
    <form noValidate onSubmit={form.onSubmit((values) => void handleSubmit(values))}>
      <Stack gap="md">
        {errorMessage && <Alert color="red">{errorMessage}</Alert>}

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

        <PermissionSelector
          selectedPermissions={values.permissions}
          onChange={(permissions) => form.setFieldValue('permissions', permissions)}
        />
        {form.errors.permissions && <Alert color="red">{form.errors.permissions}</Alert>}

        {wildcardSelected && (
          <Checkbox
            label="Acknowledge wildcard grant"
            {...form.getInputProps('acknowledgeWildcardGrant', { type: 'checkbox' })}
          />
        )}
        {form.errors.acknowledgeWildcardGrant && <Alert color="red">{form.errors.acknowledgeWildcardGrant}</Alert>}

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

function isWildcardPermission(permission: string) {
  return permission === '*' || permission.endsWith(':*');
}
