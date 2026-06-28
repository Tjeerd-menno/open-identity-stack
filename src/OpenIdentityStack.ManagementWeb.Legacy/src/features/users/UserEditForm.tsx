import { Alert, Button, Group, NativeSelect, PasswordInput, Stack, Text, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useState } from 'react';
import { DestructiveActionDialog } from '@/components/FormPrimitives';
import { firstError, maxLength, minLength, required } from '@/lib/form-validation';
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
  const profileForm = useForm({
    mode: 'controlled',
    validateInputOnBlur: true,
    initialValues: { displayName: user.displayName },
    validate: {
      displayName: (value) => firstError(
        required(value, 'Display name is required.'),
        maxLength(value, 200, 'Display name')
      ),
    },
  });
  const resetPasswordForm = useForm({
    mode: 'controlled',
    validateInputOnBlur: true,
    initialValues: { newPassword: '' },
    validate: {
      newPassword: (value) => firstError(
        required(value, 'New password is required.'),
        minLength(value, 8, 'New password'),
        maxLength(value, 256, 'New password')
      ),
    },
  });
  const assignRoleForm = useForm({
    mode: 'controlled',
    initialValues: { selectedRoleId: '' },
    validate: {
      selectedRoleId: (value) => value ? null : 'Select a role to assign.',
    },
  });
  const linkIdentityForm = useForm({
    mode: 'controlled',
    validateInputOnBlur: true,
    initialValues: { providerId: '', subject: '' },
    validate: {
      providerId: (value) => firstError(
        required(value, 'Provider ID is required.'),
        maxLength(value, 100, 'Provider ID')
      ),
      subject: (value) => firstError(
        required(value, 'Subject is required.'),
        maxLength(value, 256, 'Subject')
      ),
    },
  });

  return (
    <Stack gap="md">
      <form
        noValidate
        onSubmit={profileForm.onSubmit(async (values) => {
          setSaveError(null);
          try {
            await updateUser.mutateAsync(values.displayName.trim());
          } catch (mutationError) {
            setSaveError(mutationError instanceof Error ? mutationError.message : 'Unable to save user changes.');
          }
        })}
      >
        <Stack gap="sm">
          <TextInput
            label="Display name"
            required
            {...profileForm.getInputProps('displayName')}
          />
          <Group>
            <Button type="submit" loading={updateUser.isPending}>
              Save changes
            </Button>
          </Group>
        </Stack>
      </form>
      <Group>
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
          <form
            noValidate
            onSubmit={resetPasswordForm.onSubmit(async (values) => {
              setResetPasswordError(null);
              try {
                await resetPassword.mutateAsync(values.newPassword);
                resetPasswordForm.reset();
              } catch (mutationError) {
                setResetPasswordError(mutationError instanceof Error ? mutationError.message : 'Unable to reset password.');
              }
            })}
          >
            <Stack gap="sm">
              <PasswordInput
                label="New password"
                required
                {...resetPasswordForm.getInputProps('newPassword')}
              />
              <Group justify="flex-end">
                <Button type="submit" loading={resetPassword.isPending}>
                  Reset password
                </Button>
              </Group>
            </Stack>
          </form>
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
          <form
            noValidate
            onSubmit={assignRoleForm.onSubmit(async (values) => {
              setAssignError(null);
              try {
                await assignRole.mutateAsync(values.selectedRoleId);
                assignRoleForm.reset();
              } catch (mutationError) {
                setAssignError(mutationError instanceof Error ? mutationError.message : 'Unable to assign role.');
              }
            })}
          >
            <Group align="end">
              <NativeSelect
                label="Assign role"
                data={[
                  { value: '', label: 'Choose a role' },
                  ...availableRoles.map((role) => ({ value: role.id, label: role.displayName })),
                ]}
                {...assignRoleForm.getInputProps('selectedRoleId')}
              />
              <Button type="submit" variant="light" loading={assignRole.isPending}>
                Assign selected role
              </Button>
            </Group>
          </form>
          {assignError && <Alert color="red">{assignError}</Alert>}
        </>
      )}
      <Stack gap="sm">
        <Text fw={600}>Link upstream identity</Text>
        <form
          noValidate
          onSubmit={linkIdentityForm.onSubmit(async (values) => {
            setLinkIdentityError(null);
            try {
              await linkIdentity.mutateAsync({
                providerId: values.providerId.trim(),
                subject: values.subject.trim(),
              });
              linkIdentityForm.reset();
            } catch (mutationError) {
              setLinkIdentityError(mutationError instanceof Error ? mutationError.message : 'Unable to link identity.');
            }
          })}
        >
          <Group align="end">
            <TextInput
              label="Provider id"
              required
              {...linkIdentityForm.getInputProps('providerId')}
            />
            <TextInput
              label="Subject"
              required
              {...linkIdentityForm.getInputProps('subject')}
            />
            <Button type="submit" variant="light" loading={linkIdentity.isPending}>
              Link upstream identity
            </Button>
          </Group>
        </form>
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
