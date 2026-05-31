import { Alert, Button, Group, NativeSelect, Stack, TextInput } from '@mantine/core';
import { useState } from 'react';
import type { RoleListItem, User } from '@/lib/admin-api';
import { useAssignRoleMutation, useDisableUserMutation, useUpdateUserMutation } from './user-mutations';

type UserEditFormProps = {
  user: User;
  availableRoles: RoleListItem[];
  canDisable: boolean;
  canAssignRoles: boolean;
};

export function UserEditForm({ user, availableRoles, canDisable, canAssignRoles }: UserEditFormProps) {
  const [displayName, setDisplayName] = useState(user.displayName);
  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [disableError, setDisableError] = useState<string | null>(null);
  const [assignError, setAssignError] = useState<string | null>(null);
  const updateUser = useUpdateUserMutation(user.id);
  const disableUser = useDisableUserMutation(user.id);
  const assignRole = useAssignRoleMutation(user.id);

  return (
    <Stack gap="md">
      <TextInput
        label="Display name"
        value={displayName}
        onChange={(event) => setDisplayName(event.currentTarget.value)}
      />
      <Group>
        <Button
          onClick={async () => {
            setSaveError(null);
            try {
              await updateUser.mutateAsync(displayName);
            } catch (mutationError) {
              setSaveError(mutationError instanceof Error ? mutationError.message : 'Unable to save user changes.');
            }
          }}
          loading={updateUser.isPending}
        >
          Save changes
        </Button>
        {canDisable && (
          <Button
            color="red"
            variant="light"
            onClick={async () => {
              setDisableError(null);
              try {
                await disableUser.mutateAsync();
              } catch (mutationError) {
                setDisableError(mutationError instanceof Error ? mutationError.message : 'Unable to disable user.');
              }
            }}
            loading={disableUser.isPending}
          >
            Disable user
          </Button>
        )}
      </Group>
      {saveError && <Alert color="red">{saveError}</Alert>}
      {disableError && <Alert color="red">{disableError}</Alert>}
      {canAssignRoles && (
        <>
          <Group align="end">
            <NativeSelect
              label="Assign role"
              value={selectedRoleId ?? ''}
              onChange={(event) => setSelectedRoleId(event.currentTarget.value || null)}
              data={[
                { value: '', label: 'Choose a role' },
                ...availableRoles.map((role) => ({ value: role.id, label: role.displayName })),
              ]}
            />
            <Button
              variant="light"
              disabled={!selectedRoleId}
              onClick={async () => {
                if (!selectedRoleId) {
                  return;
                }

                setAssignError(null);
                try {
                  await assignRole.mutateAsync(selectedRoleId);
                } catch (mutationError) {
                  setAssignError(mutationError instanceof Error ? mutationError.message : 'Unable to assign role.');
                }
              }}
              loading={assignRole.isPending}
            >
              Assign selected role
            </Button>
          </Group>
          {assignError && <Alert color="red">{assignError}</Alert>}
        </>
      )}
    </Stack>
  );
}
