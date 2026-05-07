/**
 * EditProviderForm Component
 * 
 * Form component for editing identity providers
 */

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Checkbox } from '@/components/ui/checkbox';
import type { Provider, UpdateProviderRequest } from '@/types';

const updateProviderSchema = z.object({
  displayName: z.string().min(1, 'Display name is required'),
  clientId: z.string().min(1, 'Client ID is required'),
  clientSecret: z.string().optional(),
  scopes: z.string().optional(),
  jitProvisioningEnabled: z.boolean(),
});

type UpdateProviderFormData = z.infer<typeof updateProviderSchema>;

interface EditProviderFormProps {
  provider: Provider;
  onSubmit: (data: UpdateProviderRequest) => void;
  isLoading?: boolean;
}

export function EditProviderForm({ provider, onSubmit, isLoading }: EditProviderFormProps) {
  const form = useForm<UpdateProviderFormData>({
    resolver: zodResolver(updateProviderSchema),
    defaultValues: {
      displayName: provider.displayName,
      clientId: provider.clientId,
      clientSecret: '',
      scopes: provider.scopes.join(' '),
      jitProvisioningEnabled: provider.jitProvisioningEnabled,
    }
  });

  const handleFormSubmit = (data: UpdateProviderFormData) => {
    const request: UpdateProviderRequest = {
      displayName: data.displayName,
      clientId: data.clientId,
      clientSecret: data.clientSecret || undefined,
      scopes: data.scopes ? data.scopes.split(' ').filter(s => s.length > 0) : undefined,
      jitProvisioningEnabled: data.jitProvisioningEnabled,
    };
    onSubmit(request);
  };

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(handleFormSubmit)} className="space-y-6">
        <div className="space-y-2">
          <FormLabel>Name</FormLabel>
          <Input value={provider.name} disabled />
          <FormDescription>Provider name cannot be changed after creation</FormDescription>
        </div>

        <FormField
          control={form.control}
          name="displayName"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Display Name</FormLabel>
              <FormControl>
                <Input {...field} placeholder="Google" />
              </FormControl>
              <FormDescription>User-friendly name shown in the UI</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <div className="space-y-2">
          <FormLabel>Authority</FormLabel>
          <Input value={provider.authority} disabled />
          <FormDescription>Authority URL cannot be changed after creation</FormDescription>
        </div>

        <FormField
          control={form.control}
          name="clientId"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Client ID</FormLabel>
              <FormControl>
                <Input {...field} />
              </FormControl>
              <FormDescription>OAuth2 Client ID from the provider</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="clientSecret"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Client Secret</FormLabel>
              <FormControl>
                <Input {...field} type="password" placeholder="Leave empty to keep existing" />
              </FormControl>
              <FormDescription>OAuth2 Client Secret (leave empty to keep existing)</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="scopes"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Scopes</FormLabel>
              <FormControl>
                <Input {...field} placeholder="openid profile email" />
              </FormControl>
              <FormDescription>Space-separated list of OAuth2 scopes</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="jitProvisioningEnabled"
          render={({ field }) => (
            <FormItem className="flex flex-row items-start space-x-3 space-y-0 rounded-md border p-4">
              <FormControl>
                <Checkbox
                  checked={field.value}
                  onCheckedChange={field.onChange}
                />
              </FormControl>
              <div className="space-y-1 leading-none">
                <FormLabel>Just-in-Time Provisioning</FormLabel>
                <FormDescription>
                  Automatically create user accounts when users sign in for the first time
                </FormDescription>
              </div>
            </FormItem>
          )}
        />

        <div className="flex justify-end gap-4">
          <Button type="submit" disabled={isLoading}>
            {isLoading ? 'Saving...' : 'Save Changes'}
          </Button>
        </div>
      </form>
    </Form>
  );
}
