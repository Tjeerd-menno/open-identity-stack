import { useLayoutEffect, useMemo, useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { Plus, X } from 'lucide-react';
import { useForm, useWatch } from 'react-hook-form';
import { z } from 'zod';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  ApplicationClientType,
  ApplicationOptionAvailability,
  ApplicationProfile,
  type ApplicationProfilePolicy,
  type Application,
} from '@/types';
import { useApplicationProfilePolicies } from '../hooks/useApplicationProfilePolicies';

const createSchema = z.object({
  clientId: z.string().min(1, 'Client ID is required').max(100),
  displayName: z.string().min(1, 'Display name is required').max(200),
  description: z.string().optional(),
  profile: z.enum(ApplicationProfile),
  clientType: z.enum(ApplicationClientType),
  allowedGrantTypes: z.array(z.string()).min(1, 'At least one grant type is required'),
  allowedScopes: z.array(z.string()).min(1, 'At least one scope is required'),
  redirectUris: z.array(z.string().url('Must be a valid URL')),
  postLogoutRedirectUris: z.array(z.string().url('Must be a valid URL')),
  requirePkce: z.boolean(),
  requireConsent: z.boolean(),
}).refine((data) => {
  if (data.allowedGrantTypes.includes('authorization_code')) {
    return data.redirectUris.length > 0;
  }

  return true;
}, {
  message: 'At least one redirect URI is required when using authorization code flow',
  path: ['redirectUris'],
});

const updateSchema = z.object({
  displayName: z.string().min(1, 'Display name is required').max(200),
  description: z.string().optional(),
});

export type CreateApplicationFormData = z.infer<typeof createSchema>;
export type UpdateApplicationFormData = z.infer<typeof updateSchema>;

type ApplicationFormProps =
  | CreateApplicationFormProps
  | UpdateApplicationFormProps;

interface CreateApplicationFormProps {
  application?: undefined;
  onSubmit: (data: CreateApplicationFormData) => Promise<void>;
  initialProfile?: ApplicationProfile;
  isLoading?: boolean;
}

interface UpdateApplicationFormProps {
  application: Application;
  onSubmit: (data: UpdateApplicationFormData) => Promise<void>;
  isLoading?: boolean;
}

const availableScopes = ['openid', 'profile', 'email', 'api', 'offline_access'];
const applicationProfileOrder = [
  ApplicationProfile.Web,
  ApplicationProfile.SinglePage,
  ApplicationProfile.Native,
  ApplicationProfile.MachineToMachine,
  ApplicationProfile.Device,
] as const;

const applicationProfileLabels: Record<ApplicationProfile, string> = {
  [ApplicationProfile.Web]: 'Web',
  [ApplicationProfile.SinglePage]: 'Single Page',
  [ApplicationProfile.Native]: 'Native',
  [ApplicationProfile.MachineToMachine]: 'Machine-to-machine',
  [ApplicationProfile.Device]: 'Device',
  [ApplicationProfile.Custom]: 'Custom',
};

export function ApplicationForm({ application, onSubmit, isLoading, ...createProps }: ApplicationFormProps) {
  if (application) {
    return (
      <UpdateApplicationForm
        application={application}
        onSubmit={onSubmit}
        isLoading={isLoading}
      />
    );
  }

  return <CreateApplicationForm onSubmit={onSubmit} isLoading={isLoading} {...createProps} />;
}

function CreateApplicationForm({
  onSubmit,
  initialProfile,
  isLoading,
}: Omit<CreateApplicationFormProps, 'application'>) {
  const [submitError, setSubmitError] = useState<string | null>(null);
  const policiesQuery = useApplicationProfilePolicies();
  const form = useForm<CreateApplicationFormData>({
    resolver: zodResolver(createSchema),
    defaultValues: {
      clientId: '',
      displayName: '',
      description: '',
      profile: initialProfile ?? ApplicationProfile.Web,
      clientType: ApplicationClientType.Confidential,
      allowedGrantTypes: [],
      allowedScopes: [],
      redirectUris: [],
      postLogoutRedirectUris: [],
      requirePkce: true,
      requireConsent: true,
    },
  });
  const { control, getValues, handleSubmit, setValue } = form;

  const redirectUris = useWatch({ control, name: 'redirectUris' });
  const postLogoutRedirectUris = useWatch({ control, name: 'postLogoutRedirectUris' });
  const allowedScopes = useWatch({ control, name: 'allowedScopes' });
  const allowedGrantTypes = useWatch({ control, name: 'allowedGrantTypes' });
  const selectedProfile = useWatch({ control, name: 'profile' });
  const policiesByProfile = useMemo(() => new Map(
    (policiesQuery.data ?? []).map((policy) => [policy.applicationProfile, policy])
  ), [policiesQuery.data]);
  const selectedPolicy = policiesByProfile.get(selectedProfile);
  const reservedPolicies = useMemo(
    () => (policiesQuery.data ?? []).filter((policy) => !policy.isSelectable && policy.applicationProfile !== ApplicationProfile.Custom),
    [policiesQuery.data]
  );
  const optionalGrantTypes = useMemo(
    () => selectedPolicy
      ? selectedPolicy.allowedGrantTypes.filter((grantType) => !selectedPolicy.defaultGrantTypes.includes(grantType))
      : [],
    [selectedPolicy]
  );
  const optionalSelectedGrantTypes = useMemo(
    () => selectedPolicy
      ? allowedGrantTypes.filter((grantType) => !selectedPolicy.defaultGrantTypes.includes(grantType))
      : [],
    [allowedGrantTypes, selectedPolicy]
  );

  useLayoutEffect(() => {
    if (!selectedPolicy) {
      return;
    }

    if (getValues('clientType') !== selectedPolicy.defaultClientProfile) {
      setValue('clientType', selectedPolicy.defaultClientProfile, {
        shouldDirty: true,
        shouldValidate: true,
      });
    }

    const nextGrantTypes = [...selectedPolicy.defaultGrantTypes];
    if (!areSameValues(getValues('allowedGrantTypes'), nextGrantTypes)) {
      setValue('allowedGrantTypes', nextGrantTypes, {
        shouldDirty: true,
        shouldValidate: true,
      });
    }

    if (getValues('requirePkce') !== selectedPolicy.defaultRequirePkce) {
      setValue('requirePkce', selectedPolicy.defaultRequirePkce, {
        shouldDirty: true,
        shouldValidate: true,
      });
    }

    const nextRequireConsent = isOptionHidden(selectedPolicy, 'consent') ? false : selectedPolicy.defaultRequireConsent;
    if (getValues('requireConsent') !== nextRequireConsent) {
      setValue('requireConsent', nextRequireConsent, {
        shouldDirty: true,
        shouldValidate: true,
      });
    }

    const currentRedirectUris = getValues('redirectUris');
    if (selectedPolicy.requiresRedirectUris) {
      if (currentRedirectUris.length === 0) {
        setValue('redirectUris', [''], {
          shouldDirty: true,
          shouldValidate: true,
        });
      }
    } else if (currentRedirectUris.length > 0) {
      setValue('redirectUris', [], {
        shouldDirty: true,
        shouldValidate: true,
      });
    }

    const currentPostLogoutRedirectUris = getValues('postLogoutRedirectUris');
    if (isOptionHidden(selectedPolicy, 'postLogoutRedirectUris') && currentPostLogoutRedirectUris.length > 0) {
      setValue('postLogoutRedirectUris', [], {
        shouldDirty: true,
        shouldValidate: true,
      });
    }
  }, [getValues, selectedPolicy, setValue]);

  const updateArrayValue = (
    fieldName: 'redirectUris' | 'postLogoutRedirectUris',
    index: number,
    value: string
  ) => {
    const values = [...form.getValues(fieldName)];
    values[index] = value;
    form.setValue(fieldName, values, { shouldValidate: true, shouldDirty: true });
  };

  const appendValue = (fieldName: 'redirectUris' | 'postLogoutRedirectUris') => {
    form.setValue(fieldName, [...form.getValues(fieldName), ''], {
      shouldValidate: true,
      shouldDirty: true,
    });
  };

  const removeValue = (fieldName: 'redirectUris' | 'postLogoutRedirectUris', index: number) => {
    form.setValue(
      fieldName,
      form.getValues(fieldName).filter((_, valueIndex) => valueIndex !== index),
      { shouldValidate: true, shouldDirty: true }
    );
  };

  if (policiesQuery.isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Loading application policies</CardTitle>
          <CardDescription>Retrieving application profile guidance and defaults.</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  if (policiesQuery.isError || !selectedPolicy) {
    return (
      <Alert>
        <AlertDescription>
          Unable to load application profile guidance. Reload the page and try again.
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <Form {...form}>
      <form
        onSubmit={handleSubmit(async (data) => {
          setSubmitError(null);
          try {
            await onSubmit(data);
          } catch (unknownError) {
            setSubmitError(toSubmissionErrorMessage(unknownError, 'Failed to create application.'));
          }
        })}
        className="space-y-6"
      >
        {submitError && (
          <Alert variant="destructive">
            <AlertDescription>{submitError}</AlertDescription>
          </Alert>
        )}
        <Card>
          <CardHeader>
            <CardTitle>Basic Information</CardTitle>
            <CardDescription>Create a unified OAuth 2.0 / OpenID Connect application</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FormField
              control={form.control}
              name="clientId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Client ID</FormLabel>
                  <FormControl>
                    <Input {...field} placeholder="orders-web" />
                  </FormControl>
                  <FormDescription>Protocol client_id for this application</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="displayName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Display Name</FormLabel>
                  <FormControl>
                    <Input {...field} placeholder="Orders Web" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="description"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Description (Optional)</FormLabel>
                  <FormControl>
                    <Textarea {...field} placeholder="Description of this application" rows={3} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="grid gap-4 md:grid-cols-2">
              <FormField
                control={form.control}
                name="profile"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Application Profile</FormLabel>
                    <Select onValueChange={field.onChange} value={field.value}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Select application profile" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {applicationProfileOrder.map((profile) => {
                          const policy = policiesByProfile.get(profile);
                          if (!policy) {
                            return null;
                          }

                          return (
                            <SelectItem key={profile} value={profile} disabled={!policy.isSelectable}>
                              {policy.isSelectable ? applicationProfileLabels[profile] : `${applicationProfileLabels[profile]} (Reserved)`}
                            </SelectItem>
                          );
                        })}
                      </SelectContent>
                    </Select>
                    <FormDescription>Choose the application profile that matches the OAuth client profile.</FormDescription>
                    {reservedPolicies.map((policy) => (
                      <p key={policy.applicationProfile} className="text-sm text-muted-foreground">
                        {policy.unavailabilityReason}
                      </p>
                    ))}
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="clientType"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Client Profile</FormLabel>
                    <FormControl>
                      <Input {...field} readOnly />
                    </FormControl>
                    <FormDescription>This profile is fixed by the selected application profile.</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
          </CardContent>
        </Card>

        {selectedProfile === ApplicationProfile.SinglePage && (
          <Alert>
            <AlertDescription>
              Browser applications are public clients. Shared secrets and certificates stay hidden, and PKCE is always required.
            </AlertDescription>
          </Alert>
        )}

        {selectedProfile === ApplicationProfile.Native && (
          <Alert>
            <AlertDescription>
              Claimed HTTPS redirects are preferred. Private scheme and loopback redirects are supported for native applications.
            </AlertDescription>
          </Alert>
        )}

        {selectedProfile === ApplicationProfile.MachineToMachine && (
          <Alert>
            <AlertDescription>
              Machine-to-machine applications use only the client credentials grant. Redirects, PKCE, and consent do not apply.
            </AlertDescription>
          </Alert>
        )}

        {!isOptionHidden(selectedPolicy, 'redirectUris') && (
          <Card>
            <CardHeader>
              <CardTitle>Redirect URIs</CardTitle>
              <CardDescription>
                {selectedPolicy.requiresRedirectUris
                  ? 'At least one redirect URI is required for this application profile.'
                  : 'Allowed redirect URIs after authentication'}
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {redirectUris.map((uri, index) => (
                <div key={index} className="flex gap-2">
                  <Input
                    value={uri}
                    onChange={(event) => updateArrayValue('redirectUris', index, event.target.value)}
                    placeholder="https://example.com/callback"
                  />
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => removeValue('redirectUris', index)}
                  >
                    <X className="h-4 w-4" />
                  </Button>
                </div>
              ))}
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => appendValue('redirectUris')}
              >
                <Plus className="h-4 w-4 mr-2" />
                Add Redirect URI
              </Button>
              <FormField
                control={form.control}
                name="redirectUris"
                render={() => <FormMessage />}
              />
            </CardContent>
          </Card>
        )}

        {!isOptionHidden(selectedPolicy, 'postLogoutRedirectUris') && (
          <Card>
            <CardHeader>
              <CardTitle>Post Logout Redirect URIs</CardTitle>
              <CardDescription>Allowed redirect URIs after logout</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {postLogoutRedirectUris.map((uri, index) => (
                <div key={index} className="flex gap-2">
                  <Input
                    value={uri}
                    onChange={(event) => updateArrayValue('postLogoutRedirectUris', index, event.target.value)}
                    placeholder="https://example.com/"
                  />
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => removeValue('postLogoutRedirectUris', index)}
                  >
                    <X className="h-4 w-4" />
                  </Button>
                </div>
              ))}
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => appendValue('postLogoutRedirectUris')}
              >
                <Plus className="h-4 w-4 mr-2" />
                Add Post Logout Redirect URI
              </Button>
            </CardContent>
          </Card>
        )}

        <Card>
          <CardHeader>
            <CardTitle>Allowed Scopes</CardTitle>
            <CardDescription>Select which scopes this application can request</CardDescription>
          </CardHeader>
          <CardContent>
            <CheckboxGroup
              values={availableScopes}
              selectedValues={allowedScopes}
              onChange={(values) => form.setValue('allowedScopes', values, { shouldValidate: true, shouldDirty: true })}
            />
            <FormField
              control={form.control}
              name="allowedScopes"
              render={() => <FormMessage />}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Allowed Grant Types</CardTitle>
            <CardDescription>Defaults come from the selected application profile policy.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              {selectedPolicy.defaultGrantTypes.map((grantType) => (
                <div key={grantType} className="text-sm font-medium">
                  {grantType}
                </div>
              ))}
            </div>
            {optionalGrantTypes.length > 0 && (
              <CheckboxGroup
                values={optionalGrantTypes}
                selectedValues={optionalSelectedGrantTypes}
                onChange={(values) => form.setValue(
                  'allowedGrantTypes',
                  [...selectedPolicy.defaultGrantTypes, ...values],
                  { shouldValidate: true, shouldDirty: true }
                )}
              />
            )}
            <FormField
              control={form.control}
              name="allowedGrantTypes"
              render={() => <FormMessage />}
            />
          </CardContent>
        </Card>

        {(!isOptionHidden(selectedPolicy, 'pkce') || !isOptionHidden(selectedPolicy, 'consent')) && (
          <Card>
            <CardHeader>
              <CardTitle>Security Options</CardTitle>
              <CardDescription>Configure security requirements for this application</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {!isOptionHidden(selectedPolicy, 'pkce') && (
                isOptionReadOnly(selectedPolicy, 'pkce') ? (
                  <div className="space-y-1">
                    <p className="text-sm font-medium">Require PKCE</p>
                    <p className="text-sm text-muted-foreground">PKCE is required for this application profile.</p>
                  </div>
                ) : (
                  <FormField
                    control={form.control}
                    name="requirePkce"
                    render={({ field }) => (
                      <FormItem className="flex flex-row items-start space-x-3 space-y-0">
                        <FormControl>
                          <Checkbox checked={field.value} onCheckedChange={field.onChange} />
                        </FormControl>
                        <div className="space-y-1 leading-none">
                          <FormLabel>Require PKCE</FormLabel>
                          <FormDescription>Require Proof Key for Code Exchange for authorization code flow</FormDescription>
                        </div>
                      </FormItem>
                    )}
                  />
                )
              )}

              {!isOptionHidden(selectedPolicy, 'consent') && (
                <FormField
                  control={form.control}
                  name="requireConsent"
                  render={({ field }) => (
                    <FormItem className="flex flex-row items-start space-x-3 space-y-0">
                      <FormControl>
                        <Checkbox checked={field.value} onCheckedChange={field.onChange} />
                      </FormControl>
                      <div className="space-y-1 leading-none">
                        <FormLabel>Require Consent</FormLabel>
                        <FormDescription>Require user consent screen during authorization</FormDescription>
                      </div>
                    </FormItem>
                  )}
                />
              )}
            </CardContent>
          </Card>
        )}

        <div className="flex justify-end">
          <Button type="submit" disabled={isLoading}>
            Create Application
          </Button>
        </div>
      </form>
    </Form>
  );
}

function UpdateApplicationForm({
  application,
  onSubmit,
  isLoading,
}: UpdateApplicationFormProps) {
  const [submitError, setSubmitError] = useState<string | null>(null);
  const form = useForm<UpdateApplicationFormData>({
    resolver: zodResolver(updateSchema),
    defaultValues: {
      displayName: application.displayName,
      description: application.description || '',
    },
  });

  return (
    <Form {...form}>
      <form
        onSubmit={form.handleSubmit(async (data) => {
          setSubmitError(null);
          try {
            await onSubmit(data);
          } catch (unknownError) {
            setSubmitError(toSubmissionErrorMessage(unknownError, 'Failed to update application.'));
          }
        })}
        className="space-y-6"
      >
        {submitError && (
          <Alert variant="destructive">
            <AlertDescription>{submitError}</AlertDescription>
          </Alert>
        )}
        <Card>
          <CardHeader>
            <CardTitle>Basic Information</CardTitle>
            <CardDescription>Update application details</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FormField
              control={form.control}
              name="displayName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Display Name</FormLabel>
                  <FormControl>
                    <Input {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="description"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Description (Optional)</FormLabel>
                  <FormControl>
                    <Textarea {...field} rows={3} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        <div className="flex justify-end">
          <Button type="submit" disabled={isLoading}>
            Update Application
          </Button>
        </div>
      </form>
    </Form>
  );
}

interface CheckboxGroupProps {
  values: string[];
  selectedValues: string[];
  onChange: (values: string[]) => void;
}

function CheckboxGroup({ values, selectedValues, onChange }: CheckboxGroupProps) {
  return (
    <div className="space-y-2">
      {values.map((value) => (
        <div key={value} className="flex flex-row items-start space-x-3 space-y-0">
          <Checkbox
            id={value}
            checked={selectedValues.includes(value)}
            onCheckedChange={(checked) => {
              onChange(
                checked
                  ? [...selectedValues, value]
                  : selectedValues.filter((selectedValue) => selectedValue !== value)
              );
            }}
          />
          <label htmlFor={value} className="text-sm font-normal">
            {value}
          </label>
        </div>
      ))}
    </div>
  );
}

function isOptionHidden(policy: ApplicationProfilePolicy, key: string) {
  return policy.options[key] === ApplicationOptionAvailability.Hidden;
}

function isOptionReadOnly(policy: ApplicationProfilePolicy, key: string) {
  return policy.options[key] === ApplicationOptionAvailability.ReadOnly;
}

function areSameValues(left: string[], right: string[]) {
  return left.length === right.length && left.every((value, index) => value === right[index]);
}

function toSubmissionErrorMessage(unknownError: unknown, fallbackMessage: string) {
  return unknownError instanceof Error && unknownError.message
    ? unknownError.message
    : fallbackMessage;
}
