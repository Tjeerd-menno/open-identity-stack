import {
  Alert,
  Button,
  Checkbox,
  Group,
  NativeSelect,
  Stack,
  Text,
  Textarea,
  TextInput,
  Title,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useEffect } from 'react';
import { getApiErrorMessage } from '@/lib/admin-api';
import {
  ApplicationClientType,
  ApplicationOptionAvailability,
  ApplicationProfile,
  type Application,
  type ApplicationProfilePolicy,
  type ConfigureApplicationOAuthRequest,
  type CreateApplicationRequest,
  type UpdateApplicationMetadataRequest,
} from './applications-api';
import { useApplicationProfilePolicies } from './applications-hooks';

export type CreateApplicationFormData = CreateApplicationRequest;
export type UpdateApplicationFormData = UpdateApplicationMetadataRequest;

type ApplicationFormProps =
  | {
      application?: undefined;
      initialProfile?: ApplicationProfile;
      isLoading?: boolean;
      onSubmit: (data: CreateApplicationFormData) => Promise<void>;
    }
  | {
      application: Application;
      isLoading?: boolean;
      onSubmit: (data: UpdateApplicationFormData) => Promise<void>;
    };

const availableScopes = ['openid', 'profile', 'email', 'api', 'offline_access'];
const profileOptions = [
  ApplicationProfile.Web,
  ApplicationProfile.SinglePage,
  ApplicationProfile.Native,
  ApplicationProfile.MachineToMachine,
  ApplicationProfile.Device,
  ApplicationProfile.Custom,
];
const profileLabels: Record<ApplicationProfile, string> = {
  [ApplicationProfile.Web]: 'Web',
  [ApplicationProfile.SinglePage]: 'Single Page',
  [ApplicationProfile.Native]: 'Native',
  [ApplicationProfile.MachineToMachine]: 'Machine-to-machine',
  [ApplicationProfile.Device]: 'Device',
  [ApplicationProfile.Custom]: 'Custom',
};

export function ApplicationForm(props: ApplicationFormProps) {
  if (props.application) {
    return <UpdateApplicationForm {...props} />;
  }

  return <CreateApplicationForm {...props} />;
}

function CreateApplicationForm({
  initialProfile = ApplicationProfile.Web,
  isLoading = false,
  onSubmit,
}: Extract<ApplicationFormProps, { application?: undefined }>) {
  const policies = useApplicationProfilePolicies();
  const form = useForm<CreateApplicationFormData>({
    initialValues: {
      clientId: '',
      displayName: '',
      description: '',
      profile: initialProfile,
      clientType: ApplicationClientType.Confidential,
      allowedGrantTypes: [],
      allowedScopes: [],
      redirectUris: [],
      postLogoutRedirectUris: [],
      requirePkce: true,
      requireConsent: true,
    },
    validate: {
      clientId: (value) => value.trim().length === 0 ? 'Client ID is required' : null,
      displayName: (value) => value.trim().length === 0 ? 'Display name is required' : null,
      allowedScopes: (value) => value.length === 0 ? 'At least one scope is required' : null,
    },
  });
  const selectedPolicy = policies.data?.find((policy) => policy.applicationProfile === form.values.profile);

  useEffect(() => {
    if (!selectedPolicy) {
      return;
    }

    form.setValues((current) => ({
      ...current,
      clientType: selectedPolicy.defaultClientProfile,
      allowedGrantTypes: selectedPolicy.defaultGrantTypes,
      redirectUris: selectedPolicy.requiresRedirectUris && current.redirectUris.length === 0 ? [''] : current.redirectUris,
      postLogoutRedirectUris: isOptionHidden(selectedPolicy, 'postLogoutRedirectUris') ? [] : current.postLogoutRedirectUris,
      requirePkce: selectedPolicy.defaultRequirePkce,
      requireConsent: isOptionHidden(selectedPolicy, 'consent') ? false : selectedPolicy.defaultRequireConsent,
    }));
  }, [form.values.profile, selectedPolicy]);

  if (policies.isLoading) {
    return <Text>Loading application policies</Text>;
  }

  if (policies.isError || !selectedPolicy) {
    return <Alert color="red">Unable to load application profile guidance.</Alert>;
  }

  return (
    <form
      onSubmit={form.onSubmit(async (data) => {
        await onSubmit(normalizeCreateData(data, selectedPolicy));
      })}
    >
      <Stack gap="lg">
        <Stack gap="sm">
          <Title order={2}>Basic information</Title>
          <TextInput label="Client ID" placeholder="orders-web" {...form.getInputProps('clientId')} />
          <TextInput label="Display name" placeholder="Orders Web" {...form.getInputProps('displayName')} />
          <Textarea label="Description" rows={3} {...form.getInputProps('description')} />
          <Group grow align="flex-start">
            <NativeSelect
              label="Application profile"
              value={form.values.profile}
              onChange={(event) => form.setFieldValue('profile', event.currentTarget.value as ApplicationProfile)}
              data={profileOptions.map((profile) => {
                const policy = policies.data?.find((item) => item.applicationProfile === profile);
                return {
                  value: profile,
                  label: policy && !policy.isSelectable ? `${profileLabels[profile]} (Reserved)` : profileLabels[profile],
                  disabled: policy ? !policy.isSelectable : false,
                };
              })}
            />
            <TextInput label="Client profile" value={form.values.clientType} readOnly />
          </Group>
          {policies.data
            ?.filter((policy) => !policy.isSelectable && policy.unavailabilityReason)
            .map((policy) => (
              <Text key={policy.applicationProfile} size="sm" c="dimmed">
                {policy.unavailabilityReason}
              </Text>
            ))}
        </Stack>

        {form.values.profile === ApplicationProfile.SinglePage && (
          <Alert>Browser applications are public clients. PKCE is always required.</Alert>
        )}
        {form.values.profile === ApplicationProfile.Native && (
          <Alert>Claimed HTTPS redirects are preferred. Private scheme and loopback redirects are supported for native applications.</Alert>
        )}
        {form.values.profile === ApplicationProfile.MachineToMachine && (
          <Alert>Machine-to-machine applications use only the client credentials grant.</Alert>
        )}

        {!isOptionHidden(selectedPolicy, 'redirectUris') && (
          <Stack gap="sm">
            <Title order={2}>Redirect URIs</Title>
            {form.values.redirectUris.map((uri, index) => (
              <TextInput
                key={index}
                aria-label={`Redirect URI ${index + 1}`}
                value={uri}
                onChange={(event) => updateArrayValue(form, 'redirectUris', index, event.currentTarget.value)}
              />
            ))}
            <Button type="button" variant="default" onClick={() => appendArrayValue(form, 'redirectUris')}>
              Add redirect URI
            </Button>
          </Stack>
        )}

        {!isOptionHidden(selectedPolicy, 'postLogoutRedirectUris') && (
          <Stack gap="sm">
            <Title order={2}>Post Logout Redirect URIs</Title>
            {form.values.postLogoutRedirectUris.map((uri, index) => (
              <TextInput
                key={index}
                aria-label={`Post logout redirect URI ${index + 1}`}
                value={uri}
                onChange={(event) => updateArrayValue(form, 'postLogoutRedirectUris', index, event.currentTarget.value)}
              />
            ))}
            <Button type="button" variant="default" onClick={() => appendArrayValue(form, 'postLogoutRedirectUris')}>
              Add post logout redirect URI
            </Button>
          </Stack>
        )}

        <Stack gap="sm">
          <Title order={2}>Allowed scopes</Title>
          {availableScopes.map((scope) => (
            <Checkbox
              key={scope}
              label={scope}
              checked={form.values.allowedScopes.includes(scope)}
              onChange={(event) => toggleValue(form, 'allowedScopes', scope, event.currentTarget.checked)}
            />
          ))}
          {form.errors.allowedScopes && <Text c="red">{form.errors.allowedScopes}</Text>}
        </Stack>

        <Stack gap="sm">
          <Title order={2}>Allowed grant types</Title>
          {selectedPolicy.defaultGrantTypes.map((grantType) => (
            <Text key={grantType} fw={500}>{grantType}</Text>
          ))}
          {selectedPolicy.allowedGrantTypes
            .filter((grantType) => !selectedPolicy.defaultGrantTypes.includes(grantType))
            .map((grantType) => (
              <Checkbox
                key={grantType}
                label={grantType}
                checked={form.values.allowedGrantTypes.includes(grantType)}
                onChange={(event) => toggleValue(form, 'allowedGrantTypes', grantType, event.currentTarget.checked)}
              />
            ))}
        </Stack>

        {(!isOptionHidden(selectedPolicy, 'pkce') || !isOptionHidden(selectedPolicy, 'consent')) && (
          <Stack gap="sm">
            <Title order={2}>Security options</Title>
            {!isOptionHidden(selectedPolicy, 'pkce') && (
              <Checkbox
                label="Require PKCE"
                checked={form.values.requirePkce}
                disabled={isOptionReadOnly(selectedPolicy, 'pkce')}
                onChange={(event) => form.setFieldValue('requirePkce', event.currentTarget.checked)}
              />
            )}
            {!isOptionHidden(selectedPolicy, 'consent') && (
              <Checkbox
                label="Require consent"
                checked={form.values.requireConsent}
                onChange={(event) => form.setFieldValue('requireConsent', event.currentTarget.checked)}
              />
            )}
          </Stack>
        )}

        <Group justify="flex-end">
          <Button type="submit" loading={isLoading}>Create application</Button>
        </Group>
      </Stack>
    </form>
  );
}

function UpdateApplicationForm({
  application,
  isLoading = false,
  onSubmit,
}: Extract<ApplicationFormProps, { application: Application }>) {
  const form = useForm<UpdateApplicationMetadataRequest>({
    initialValues: {
      displayName: application.displayName,
      description: application.description ?? '',
    },
    validate: {
      displayName: (value) => value.trim().length === 0 ? 'Display name is required' : null,
    },
  });

  return (
    <form onSubmit={form.onSubmit(async (data) => onSubmit(data))}>
      <Stack gap="lg">
        <Title order={2}>Basic information</Title>
        <TextInput label="Display name" {...form.getInputProps('displayName')} />
        <Textarea label="Description" rows={3} {...form.getInputProps('description')} />
        <Group justify="flex-end">
          <Button type="submit" loading={isLoading}>Update application</Button>
        </Group>
      </Stack>
    </form>
  );
}

type ArrayField = 'redirectUris' | 'postLogoutRedirectUris';
type MultiSelectField = 'allowedScopes' | 'allowedGrantTypes';

type FormApi = ReturnType<typeof useForm<CreateApplicationFormData>>;

function updateArrayValue(form: FormApi, field: ArrayField, index: number, value: string) {
  const values = [...form.values[field]];
  values[index] = value;
  form.setFieldValue(field, values);
}

function appendArrayValue(form: FormApi, field: ArrayField) {
  form.setFieldValue(field, [...form.values[field], '']);
}

function toggleValue(form: FormApi, field: MultiSelectField, value: string, checked: boolean) {
  form.setFieldValue(
    field,
    checked
      ? [...form.values[field], value]
      : form.values[field].filter((item) => item !== value)
  );
}

function normalizeCreateData(data: CreateApplicationFormData, policy: ApplicationProfilePolicy): CreateApplicationFormData {
  return {
    ...data,
    description: data.description || null,
    allowedGrantTypes: policy.defaultGrantTypes.concat(
      data.allowedGrantTypes.filter((grantType) => !policy.defaultGrantTypes.includes(grantType))
    ),
    redirectUris: isOptionHidden(policy, 'redirectUris') ? [] : data.redirectUris.filter(Boolean),
    postLogoutRedirectUris: isOptionHidden(policy, 'postLogoutRedirectUris') ? [] : data.postLogoutRedirectUris.filter(Boolean),
    requirePkce: isOptionHidden(policy, 'pkce') ? false : data.requirePkce,
    requireConsent: isOptionHidden(policy, 'consent') ? false : data.requireConsent,
  };
}

function isOptionHidden(policy: ApplicationProfilePolicy, key: string) {
  return policy.options[key] === ApplicationOptionAvailability.Hidden;
}

function isOptionReadOnly(policy: ApplicationProfilePolicy, key: string) {
  return policy.options[key] === ApplicationOptionAvailability.ReadOnly;
}

export function toApplicationFormError(error: unknown) {
  return getApiErrorMessage(error);
}
