import { Alert, Button, Group as MantineGroup, Stack, Textarea, TextInput } from '@mantine/core';
import { FormEvent, useState } from 'react';
import { getApiErrorMessage } from '@/lib/admin-api';
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
  const [name, setName] = useState(group?.name ?? '');
  const [description, setDescription] = useState(group?.description ?? '');
  const [validationError, setValidationError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (name.trim().length === 0) {
      setValidationError('Group name is required.');
      return;
    }

    if (name.trim().length > 256) {
      setValidationError('Group name must be 256 characters or fewer.');
      return;
    }

    if (description.length > 1024) {
      setValidationError('Description must be 1024 characters or fewer.');
      return;
    }

    setValidationError(null);
    await onSubmit({
      name: name.trim(),
      description: description.trim() || null,
    });
  }

  return (
    <form onSubmit={(event) => void handleSubmit(event)}>
      <Stack gap="md">
        {(validationError || error) && (
          <Alert color="red">{validationError ?? getApiErrorMessage(error)}</Alert>
        )}

        <TextInput
          label="Group name"
          value={name}
          onChange={(event) => setName(event.currentTarget.value)}
          required
        />
        <Textarea
          label="Description"
          value={description}
          onChange={(event) => setDescription(event.currentTarget.value)}
          minRows={4}
          autosize
        />

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
