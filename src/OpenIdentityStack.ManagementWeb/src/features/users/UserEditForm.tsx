import { Alert, Button, Group, NativeSelect, PasswordInput, Stack, Text, TextInput } from '@mantine/core';
import { useState } from 'react';
import { ActionBar, DestructiveActionDialog } from '@/components/FormPrimitives';
import type { RoleListItem, UpstreamIdentity, User } from './users-api';
import {
  useAssignRoleMutation,
  useDeleteUserMutation,
  useDisableUserMutation,
  useEnableUserMutation,
  useLinkUpstreamIdentityMutation,
  useResetUserPasswordMutation,
  useUnassignRoleMutation,
  useUnlinkUpstreamIdentityMutation,
  useUpdateUserMutation,
} from './user-mutations';

type UserEditFormProps = {
  user: User;
  assignedRoles: RoleListItem[];
  upstreamIdentities: UpstreamIdentity[];
  availableRoles: RoleListItem[];
  canDisable: boolean;
  canDelete: boolean;
  canResetPassword: boolean;
  canAssignRoles: boolean;
};

export function UserEditForm({
  user,
  assignedRoles,
  upstreamIdentities,
  availableRoles,
  canDisable,
  canDelete,
  canResetPassword,
  canAssignRoles,
}: UserEditFormProps) {
  const [displayName, setDisplayName] = useState(user.displayName);
  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null);
  const [newPassword, setNewPassword] = useState('');
  const [providerId, setProviderId] = useState('');
  const [subject, setSubject] = useState('');
  const [deleteDialogOpened, setDeleteDialogOpened] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [disableError, setDisableError] = useState<string | null>(null);
  const [enableError, setEnableError] = useState<string | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [resetPasswordError, setResetPasswordError] = useState<string | null>(null);
  const [assignError, setAssignError] = useState<string | null>(null);
  const [unassignError, setUnassignError] = useState<string | null>(null);
  const [linkIdentityError, setLinkIdentityError] = useState<string | null>(null);
  const [unlinkIdentityError, setUnlinkIdentityError] = useState<string | null>(null);
  const updateUser = useUpdateUserMutation(user.id);
  const disableUser = useDisableUserMutation(user.id);
  const enableUser = useEnableUserMutation(user.id);
  const deleteUser = useDeleteUserMutation(user.id);
  const resetPassword = useResetUserPasswordMutation(user.id);
  const assignRole = useAssignRoleMutation(user.id);
  const unassignRole = useUnassignRoleMutation(user.id);
  const linkIdentity = useLinkUpstreamIdentityMutation(user.id);
  const unlinkIdentity = useUnlinkUpstreamIdentityMutation(user.id);

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
        {canDisable && user.status === 'Active' && (
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
        {user.status === 'Disabled' && (
          <Button
            variant="light"
            onClick={async () => {
              setEnableError(null);
              try {
                await enableUser.mutateAsync();
              } catch (mutationError) {
                setEnableError(mutationError instanceof Error ? mutationError.message : 'Unable to enable user.');
              }
            }}
            loading={enableUser.isPending}
          >
            Enable user
          </Button>
        )}
        {canDelete && (
          <Button color="red" variant="outline" onClick={() => setDeleteDialogOpened(true)}>
            Delete user
          </Button>
        )}
      </Group>
      {saveError && <Alert color="red">{saveError}</Alert>}
      {disableError && <Alert color="red">{disableError}</Alert>}
      {enableError && <Alert color="red">{enableError}</Alert>}
      {deleteError && <Alert color="red">{deleteError}</Alert>}
      {canResetPassword && (
        <Stack gap="sm">
          <Text fw={600}>Reset password</Text>
          <PasswordInput
            label="New password"
            value={newPassword}
            onChange={(event) => setNewPassword(event.currentTarget.value)}
          />
          <ActionBar
            submitLabel="Reset password"
            isSubmitting={resetPassword.isPending}
            onSubmit={async () => {
              setResetPasswordError(null);
              try {
                await resetPassword.mutateAsync(newPassword);
                setNewPassword('');
              } catch (mutationError) {
                setResetPasswordError(mutationError instanceof Error ? mutationError.message : 'Unable to reset password.');
              }
            }}
          />
          {resetPasswordError && <Alert color="red">{resetPasswordError}</Alert>}
        </Stack>
      )}
      {canAssignRoles && (
        <>
          {assignedRoles.length > 0 && (
            <Stack gap="xs">
              <Text fw={600}>Assigned role actions</Text>
              {assignedRoles.map((role) => (
                <Button
                  key={role.id}
                  variant="light"
                  color="red"
                  onClick={async () => {
                    setUnassignError(null);
                    try {
                      await unassignRole.mutateAsync(role.id);
                    } catch (mutationError) {
                      setUnassignError(mutationError instanceof Error ? mutationError.message : 'Unable to unassign role.');
                    }
                  }}
                  loading={unassignRole.isPending}
                >
                  Unassign {role.displayName}
                </Button>
              ))}
            </Stack>
          )}
          {unassignError && <Alert color="red">{unassignError}</Alert>}
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
      <Stack gap="sm">
        <Text fw={600}>Link upstream identity</Text>
        <Group align="end">
          <TextInput
            label="Provider id"
            value={providerId}
            onChange={(event) => setProviderId(event.currentTarget.value)}
          />
          <TextInput
            label="Subject"
            value={subject}
            onChange={(event) => setSubject(event.currentTarget.value)}
          />
          <Button
            variant="light"
            disabled={!providerId || !subject}
            onClick={async () => {
              setLinkIdentityError(null);
              try {
                await linkIdentity.mutateAsync({ providerId, subject });
                setProviderId('');
                setSubject('');
              } catch (mutationError) {
                setLinkIdentityError(mutationError instanceof Error ? mutationError.message : 'Unable to link identity.');
              }
            }}
            loading={linkIdentity.isPending}
          >
            Link upstream identity
          </Button>
        </Group>
        {linkIdentityError && <Alert color="red">{linkIdentityError}</Alert>}
        {upstreamIdentities.map((identity) => (
          <Button
            key={identity.providerId}
            variant="light"
            color="red"
            onClick={async () => {
              setUnlinkIdentityError(null);
              try {
                await unlinkIdentity.mutateAsync(identity.providerId);
              } catch (mutationError) {
                setUnlinkIdentityError(mutationError instanceof Error ? mutationError.message : 'Unable to unlink identity.');
              }
            }}
            loading={unlinkIdentity.isPending}
          >
            Unlink {identity.providerName ?? identity.providerId}
          </Button>
        ))}
        {unlinkIdentityError && <Alert color="red">{unlinkIdentityError}</Alert>}
      </Stack>
      <DestructiveActionDialog
        opened={deleteDialogOpened}
        onOpenChange={setDeleteDialogOpened}
        subject={user.displayName}
        message={`Permanently delete ${user.email}. This action cannot be undone.`}
        loading={deleteUser.isPending}
        onConfirm={async () => {
          setDeleteError(null);
          try {
            await deleteUser.mutateAsync();
            setDeleteDialogOpened(false);
          } catch (mutationError) {
            setDeleteError(mutationError instanceof Error ? mutationError.message : 'Unable to delete user.');
          }
        }}
      />
    </Stack>
  );
}
