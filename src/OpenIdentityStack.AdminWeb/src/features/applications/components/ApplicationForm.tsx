import { zodResolver } from '@hookform/resolvers/zod';
import { Plus, X } from 'lucide-react';
import { useForm, useWatch } from 'react-hook-form';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  ApplicationClientType,
  ApplicationType,
  type Application,
} from '@/types';

const createSchema = z.object({
  clientId: z.string().min(1, 'Client ID is required').max(100),
  displayName: z.string().min(1, 'Display name is required').max(200),
  description: z.string().optional(),
  type: z.enum(ApplicationType),
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
  isLoading?: boolean;
}

interface UpdateApplicationFormProps {
  application: Application;
  onSubmit: (data: UpdateApplicationFormData) => Promise<void>;
  isLoading?: boolean;
}

const availableScopes = ['openid', 'profile', 'email', 'api', 'offline_access'];
const availableGrantTypes = ['authorization_code', 'client_credentials', 'refresh_token', 'device_code'];

export function ApplicationForm({ application, onSubmit, isLoading }: ApplicationFormProps) {
  if (application) {
    return (
      <UpdateApplicationForm
        application={application}
        onSubmit={onSubmit}
        isLoading={isLoading}
      />
    );
  }

  return <CreateApplicationForm onSubmit={onSubmit} isLoading={isLoading} />;
}

function CreateApplicationForm({
  onSubmit,
  isLoading,
}: Omit<CreateApplicationFormProps, 'application'>) {
  const form = useForm<CreateApplicationFormData>({
    resolver: zodResolver(createSchema),
    defaultValues: {
      clientId: '',
      displayName: '',
      description: '',
      type: ApplicationType.Web,
      clientType: ApplicationClientType.Confidential,
      allowedGrantTypes: [],
      allowedScopes: [],
      redirectUris: [],
      postLogoutRedirectUris: [],
      requirePkce: false,
      requireConsent: true,
    },
  });

  const redirectUris = useWatch({ control: form.control, name: 'redirectUris' });
  const postLogoutRedirectUris = useWatch({ control: form.control, name: 'postLogoutRedirectUris' });
  const allowedScopes = useWatch({ control: form.control, name: 'allowedScopes' });
  const allowedGrantTypes = useWatch({ control: form.control, name: 'allowedGrantTypes' });

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

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
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
                name="type"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Application Type</FormLabel>
                    <Select onValueChange={field.onChange} defaultValue={field.value}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Select application type" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value={ApplicationType.Web}>Web</SelectItem>
                        <SelectItem value={ApplicationType.SinglePage}>Single Page</SelectItem>
                        <SelectItem value={ApplicationType.Native}>Native</SelectItem>
                        <SelectItem value={ApplicationType.MachineToMachine}>Machine-to-machine</SelectItem>
                        <SelectItem value={ApplicationType.Device}>Device</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="clientType"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Client Type</FormLabel>
                    <Select onValueChange={field.onChange} defaultValue={field.value}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Select client type" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value={ApplicationClientType.Confidential}>Confidential</SelectItem>
                        <SelectItem value={ApplicationClientType.Public}>Public</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Redirect URIs</CardTitle>
            <CardDescription>Allowed redirect URIs after authentication</CardDescription>
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
            <CardDescription>Select which OAuth2 grant types this application can use</CardDescription>
          </CardHeader>
          <CardContent>
            <CheckboxGroup
              values={availableGrantTypes}
              selectedValues={allowedGrantTypes}
              onChange={(values) => form.setValue('allowedGrantTypes', values, { shouldValidate: true, shouldDirty: true })}
            />
            <FormField
              control={form.control}
              name="allowedGrantTypes"
              render={() => <FormMessage />}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Security Options</CardTitle>
            <CardDescription>Configure security requirements for this application</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
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
          </CardContent>
        </Card>

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
  const form = useForm<UpdateApplicationFormData>({
    resolver: zodResolver(updateSchema),
    defaultValues: {
      displayName: application.displayName,
      description: application.description || '',
    },
  });

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
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
