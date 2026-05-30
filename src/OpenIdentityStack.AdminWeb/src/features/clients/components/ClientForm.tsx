/**
 * ClientForm Component
 * 
 * Form for creating or updating clients
 */

import { useForm, useWatch, type Resolver } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import type { Client, ClientType } from '@/types';
import { Plus, X } from 'lucide-react';

const createSchema = z.object({
  clientId: z.string().min(1, 'Client ID is required').max(100),
  displayName: z.string().min(1, 'Display name is required').max(200),
  clientType: z.number().int().min(0).max(1),
  description: z.string().optional(),
  redirectUris: z.array(z.string().url('Must be a valid URL')).default([]),
  postLogoutRedirectUris: z.array(z.string().url('Must be a valid URL')).default([]),
  allowedScopes: z.array(z.string()).min(1, 'At least one scope is required'),
  allowedGrantTypes: z.array(z.string()).min(1, 'At least one grant type is required'),
  requirePkce: z.boolean(),
  requireConsent: z.boolean(),
}).refine((data) => {
  // If authorization_code is selected, at least one redirect URI must be provided
  if (data.allowedGrantTypes.includes('authorization_code')) {
    return data.redirectUris.length > 0;
  }
  return true;
}, {
  message: 'At least one redirect URI is required when using authorization code flow',
  path: ['redirectUris'],
});

const editSchema = z.object({
  clientId: z.string(),
  displayName: z.string().min(1, 'Display name is required').max(200),
  clientType: z.number().int().min(0).max(1),
  description: z.string().optional(),
  redirectUris: z.array(z.string()).default([]),
  postLogoutRedirectUris: z.array(z.string()).default([]),
  allowedScopes: z.array(z.string()).default([]),
  allowedGrantTypes: z.array(z.string()).default([]),
  requirePkce: z.boolean(),
  requireConsent: z.boolean(),
});

export type CreateClientFormData = z.infer<typeof createSchema>;
export interface UpdateClientFormData {
  displayName: string;
  description?: string;
}
type ClientFormValues = z.infer<typeof editSchema>;

interface CreateClientFormProps {
  client?: undefined;
  onSubmit: (data: CreateClientFormData) => Promise<void>;
  isLoading?: boolean;
}

interface EditClientFormProps {
  client: Client;
  onSubmit: (data: UpdateClientFormData) => Promise<void>;
  isLoading?: boolean;
}

type ClientFormProps = CreateClientFormProps | EditClientFormProps;

const availableScopes = ['openid', 'profile', 'email', 'offline_access', 'roles'];
const availableGrantTypes = ['authorization_code', 'client_credentials', 'refresh_token', 'implicit'];

export function ClientForm(props: ClientFormProps) {
  const { isLoading } = props;
  const client = props.client;
  const isEdit = !!client;
  const resolver = (isEdit ? zodResolver(editSchema) : zodResolver(createSchema)) as Resolver<ClientFormValues>;

  const form = useForm<ClientFormValues>({
    resolver,
    defaultValues: client
      ? {
          clientId: client.clientIdValue,
          displayName: client.displayName,
          clientType: client.clientType,
          description: client.description || '',
          redirectUris: client.redirectUris,
          postLogoutRedirectUris: client.postLogoutRedirectUris,
          allowedScopes: client.allowedScopes,
          allowedGrantTypes: client.allowedGrantTypes,
          requirePkce: client.requirePkce,
          requireConsent: client.requireConsent,
        }
      : {
          clientId: '',
          displayName: '',
          clientType: 0 as ClientType,
          description: '',
          redirectUris: [],
          postLogoutRedirectUris: [],
          allowedScopes: [],
          allowedGrantTypes: [],
          requirePkce: false,
          requireConsent: true,
        },
  });

  const redirectUriFields = useWatch({ control: form.control, name: 'redirectUris' }) ?? [];
  const postLogoutUriFields = useWatch({ control: form.control, name: 'postLogoutRedirectUris' }) ?? [];

  const appendRedirectUri = () => {
    form.setValue('redirectUris', [...redirectUriFields, ''], {
      shouldDirty: true,
      shouldValidate: true,
    });
  };

  const removeRedirectUri = (index: number) => {
    form.setValue(
      'redirectUris',
      redirectUriFields.filter((_, currentIndex) => currentIndex !== index),
      {
        shouldDirty: true,
        shouldValidate: true,
      }
    );
  };

  const appendPostLogoutUri = () => {
    form.setValue('postLogoutRedirectUris', [...postLogoutUriFields, ''], {
      shouldDirty: true,
      shouldValidate: true,
    });
  };

  const removePostLogoutUri = (index: number) => {
    form.setValue(
      'postLogoutRedirectUris',
      postLogoutUriFields.filter((_, currentIndex) => currentIndex !== index),
      {
        shouldDirty: true,
        shouldValidate: true,
      }
    );
  };

  const handleFormSubmit = async (data: ClientFormValues) => {
    if (props.client) {
      await props.onSubmit({
        displayName: data.displayName,
        description: data.description,
      });
      return;
    }

    await props.onSubmit(data);
  };

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(handleFormSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Basic Information</CardTitle>
            <CardDescription>
              {isEdit ? 'Update client details' : 'Create a new OAuth 2.0 / OpenID Connect client'}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {!isEdit && (
              <FormField
                control={form.control}
                name="clientId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Client ID</FormLabel>
                    <FormControl>
                      <Input {...field} placeholder="my-web-app" />
                    </FormControl>
                    <FormDescription>
                      Unique identifier for this client (cannot be changed after creation)
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}

            <FormField
              control={form.control}
              name="displayName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Display Name</FormLabel>
                  <FormControl>
                    <Input {...field} placeholder="My Web Application" />
                  </FormControl>
                  <FormDescription>
                    Human-readable name for this client
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            {!isEdit && (
              <FormField
                control={form.control}
                name="clientType"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Client Type</FormLabel>
                    <Select 
                      onValueChange={(value) => field.onChange(Number.parseInt(value, 10))} 
                      defaultValue={field.value?.toString()}
                    >
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Select client type" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value="0">Confidential</SelectItem>
                        <SelectItem value="1">Public</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormDescription>
                      Confidential clients can securely store secrets, public clients cannot
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}

            <FormField
              control={form.control}
              name="description"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Description (Optional)</FormLabel>
                  <FormControl>
                    <Textarea {...field} placeholder="Description of this client" rows={3} />
                  </FormControl>
                  <FormDescription>
                    Additional information about this client
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        {!isEdit && (
          <>
            {/* Redirect URIs */}
            <Card>
              <CardHeader>
                <CardTitle>Redirect URIs</CardTitle>
                <CardDescription>
                  Allowed redirect URIs after authentication
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                {redirectUriFields.map((_, index) => (
                  <FormField
                    key={`redirect-uri-${index}`}
                    control={form.control}
                    name={`redirectUris.${index}`}
                    render={({ field }) => (
                      <FormItem>
                        <div className="flex gap-2">
                          <FormControl>
                            <Input {...field} placeholder="https://example.com/callback" />
                          </FormControl>
                          <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            onClick={() => removeRedirectUri(index)}
                          >
                            <X className="h-4 w-4" />
                          </Button>
                        </div>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                ))}
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={appendRedirectUri}
                >
                  <Plus className="h-4 w-4 mr-2" />
                  Add Redirect URI
                </Button>
              </CardContent>
            </Card>

            {/* Post Logout Redirect URIs */}
            <Card>
              <CardHeader>
                <CardTitle>Post Logout Redirect URIs</CardTitle>
                <CardDescription>
                  Allowed redirect URIs after logout
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                {postLogoutUriFields.map((_, index) => (
                  <FormField
                    key={`post-logout-uri-${index}`}
                    control={form.control}
                    name={`postLogoutRedirectUris.${index}`}
                    render={({ field }) => (
                      <FormItem>
                        <div className="flex gap-2">
                          <FormControl>
                            <Input {...field} placeholder="https://example.com/logout" />
                          </FormControl>
                          <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            onClick={() => removePostLogoutUri(index)}
                          >
                            <X className="h-4 w-4" />
                          </Button>
                        </div>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                ))}
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={appendPostLogoutUri}
                >
                  <Plus className="h-4 w-4 mr-2" />
                  Add Post Logout Redirect URI
                </Button>
              </CardContent>
            </Card>

            {/* Allowed Scopes */}
            <Card>
              <CardHeader>
                <CardTitle>Allowed Scopes</CardTitle>
                <CardDescription>
                  Select which scopes this client can request
                </CardDescription>
              </CardHeader>
              <CardContent>
                <FormField
                  control={form.control}
                  name="allowedScopes"
                  render={() => (
                    <FormItem>
                      <div className="space-y-2">
                        {availableScopes.map((scope) => (
                          <FormField
                            key={scope}
                            control={form.control}
                            name="allowedScopes"
                            render={({ field }) => {
                              return (
                                <FormItem
                                  key={scope}
                                  className="flex flex-row items-start space-x-3 space-y-0"
                                >
                                  <FormControl>
                                    <Checkbox
                                      checked={field.value?.includes(scope)}
                                      onCheckedChange={(checked) => {
                                        return checked
                                          ? field.onChange([...field.value, scope])
                                          : field.onChange(
                                              field.value?.filter((value) => value !== scope)
                                            );
                                      }}
                                    />
                                  </FormControl>
                                  <FormLabel className="font-normal">{scope}</FormLabel>
                                </FormItem>
                              );
                            }}
                          />
                        ))}
                      </div>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </CardContent>
            </Card>

            {/* Allowed Grant Types */}
            <Card>
              <CardHeader>
                <CardTitle>Allowed Grant Types</CardTitle>
                <CardDescription>
                  Select which OAuth2 grant types this client can use
                </CardDescription>
              </CardHeader>
              <CardContent>
                <FormField
                  control={form.control}
                  name="allowedGrantTypes"
                  render={() => (
                    <FormItem>
                      <div className="space-y-2">
                        {availableGrantTypes.map((grantType) => (
                          <FormField
                            key={grantType}
                            control={form.control}
                            name="allowedGrantTypes"
                            render={({ field }) => {
                              return (
                                <FormItem
                                  key={grantType}
                                  className="flex flex-row items-start space-x-3 space-y-0"
                                >
                                  <FormControl>
                                    <Checkbox
                                      checked={field.value?.includes(grantType)}
                                      onCheckedChange={(checked) => {
                                        return checked
                                          ? field.onChange([...field.value, grantType])
                                          : field.onChange(
                                              field.value?.filter((value) => value !== grantType)
                                            );
                                      }}
                                    />
                                  </FormControl>
                                  <FormLabel className="font-normal">{grantType}</FormLabel>
                                </FormItem>
                              );
                            }}
                          />
                        ))}
                      </div>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </CardContent>
            </Card>

            {/* Security Options */}
            <Card>
              <CardHeader>
                <CardTitle>Security Options</CardTitle>
                <CardDescription>
                  Configure security requirements for this client
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <FormField
                  control={form.control}
                  name="requirePkce"
                  render={({ field }) => (
                    <FormItem className="flex flex-row items-start space-x-3 space-y-0">
                      <FormControl>
                        <Checkbox
                          checked={field.value}
                          onCheckedChange={field.onChange}
                        />
                      </FormControl>
                      <div className="space-y-1 leading-none">
                        <FormLabel>Require PKCE</FormLabel>
                        <FormDescription>
                          Require Proof Key for Code Exchange for authorization code flow
                        </FormDescription>
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
                        <Checkbox
                          checked={field.value}
                          onCheckedChange={field.onChange}
                        />
                      </FormControl>
                      <div className="space-y-1 leading-none">
                        <FormLabel>Require Consent</FormLabel>
                        <FormDescription>
                          Require user consent screen during authorization
                        </FormDescription>
                      </div>
                    </FormItem>
                  )}
                />
              </CardContent>
            </Card>
          </>
        )}

        <div className="flex justify-end gap-2">
          <Button type="submit" disabled={isLoading}>
            {isEdit ? 'Update Client' : 'Create Client'}
          </Button>
        </div>
      </form>
    </Form>
  );
}
