/**
 * ServiceAccountForm Component
 * 
 * Form for creating or updating service accounts
 */

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Checkbox } from '@/components/ui/checkbox';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import type { ServiceAccount } from '@/types';

const createSchema = z.object({
  clientId: z.string().min(1, 'Client ID is required').max(100),
  displayName: z.string().min(1, 'Display name is required').max(200),
  allowedScopes: z.array(z.string()).min(1, 'At least one scope is required'),
  allowedGrantTypes: z.array(z.string()).min(1, 'At least one grant type is required'),
});

const updateSchema = z.object({
  displayName: z.string().min(1, 'Display name is required').max(200),
  allowedScopes: z.array(z.string()).min(1, 'At least one scope is required'),
  allowedGrantTypes: z.array(z.string()).min(1, 'At least one grant type is required'),
});

type CreateFormData = z.infer<typeof createSchema>;
type UpdateFormData = z.infer<typeof updateSchema>;

interface ServiceAccountFormProps {
  serviceAccount?: ServiceAccount;
  onSubmit: (data: CreateFormData | UpdateFormData) => Promise<void>;
  isLoading?: boolean;
}

const availableScopes = ['api', 'openid', 'profile', 'email', 'roles'];
const availableGrantTypes = ['client_credentials', 'authorization_code', 'refresh_token'];

export function ServiceAccountForm({ serviceAccount, onSubmit, isLoading }: ServiceAccountFormProps) {
  const isEdit = !!serviceAccount;
  const schema = isEdit ? updateSchema : createSchema;

  const form = useForm<CreateFormData | UpdateFormData>({
    resolver: zodResolver(schema),
    defaultValues: serviceAccount
      ? {
          displayName: serviceAccount.displayName,
          allowedScopes: serviceAccount.allowedScopes,
          allowedGrantTypes: serviceAccount.allowedGrantTypes,
        }
      : {
          clientId: '',
          displayName: '',
          allowedScopes: [],
          allowedGrantTypes: ['client_credentials'],
        },
  });

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Basic Information</CardTitle>
            <CardDescription>
              {isEdit ? 'Update service account details' : 'Create a new service account for API access'}
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
                      <Input {...field} placeholder="my-service-client" />
                    </FormControl>
                    <FormDescription>
                      Unique identifier for this service account (cannot be changed after creation)
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
                    <Input {...field} placeholder="My Service Application" />
                  </FormControl>
                  <FormDescription>
                    Human-readable name for this service account
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Allowed Scopes</CardTitle>
            <CardDescription>
              Select which scopes this service account can request
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

        <Card>
          <CardHeader>
            <CardTitle>Allowed Grant Types</CardTitle>
            <CardDescription>
              Select which OAuth2 grant types this service account can use
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

        <div className="flex justify-end gap-2">
          <Button type="submit" disabled={isLoading}>
            {isEdit ? 'Update Service Account' : 'Create Service Account'}
          </Button>
        </div>
      </form>
    </Form>
  );
}
