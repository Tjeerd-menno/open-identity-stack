import { Alert, Button, Group as MantineGroup, Stack, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { FormSection } from '@/components/FormPrimitives';
import { getApiErrorMessage } from '@/lib/admin-api';
import { firstError, maxLength, required } from '@/lib/form-validation';
import type { CreateGroupRequest, Group as GroupModel, UpdateGroupRequest } from './groups-api';

type GroupFormProps = {
  group?: GroupModel;
  mode: 'create' | 'edit';
  error?: unknown;
  loading?: boolean;
  onSubmit: (data: CreateGroupRequest | UpdateGroupRequest) => Promise<void> | void;
  onCancel: () => void;
};

export function GroupForm({ group, mode, error, loading = false, onSubmit, onCancel }: GroupFormProps) {
  const form = useForm({
    mode: 'controlled',
    validateInputOnBlur: true,
    initialValues: {
      name: group?.name ?? '',
      description: group?.description ?? '',
    },
    validate: {
      name: (value) => firstError(
        required(value, 'Group name is required.'),
        maxLength(value, 256, 'Group name')
      ),
      description: (value) => maxLength(value, 1024, 'Description'),
    },
  });
  const errorMessage = error ? getApiErrorMessage(error) : null;

  async function handleSubmit(values: typeof form.values) {
    await onSubmit({
      name: values.name.trim(),
      description: values.description.trim() || null,
    });
  }

  return (
    <form noValidate onSubmit={form.onSubmit((values) => void handleSubmit(values))}>
      <Stack gap="md">
        {errorMessage && <Alert color="red">{errorMessage}</Alert>}

        <FormSection title="Group details">
          <TextInput
            label="Group name"
            required
            {...form.getInputProps('name')}
          />
          <Textarea
            label="Description"
            minRows={4}
            autosize
            {...form.getInputProps('description')}
          />
        </FormSection>

        <MantineGroup justify="flex-end">
          <Button type="button" variant="default" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" loading={loading}>
            {mode === 'create' ? 'Create group' : 'Save group'}
          </Button>
        </MantineGroup>
      </Stack>
    </form>
  );
}
