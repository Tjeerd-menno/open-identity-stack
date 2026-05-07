import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Checkbox } from '@/components/ui/checkbox';
import type { CreateProviderRequest } from '@/types';

const providerSchema = z.object({
  name: z.string().min(1, 'Name is required').regex(/^[a-z0-9-]+$/, 'Name must be lowercase alphanumeric with hyphens'),
  displayName: z.string().min(1, 'Display name is required'),
  authority: z.string().url('Authority must be a valid URL'),
  clientId: z.string().min(1, 'Client ID is required'),
  clientSecret: z.string().optional(),
  scopes: z.string().optional(),
  jitProvisioningEnabled: z.boolean(),
});

type ProviderFormData = z.infer<typeof providerSchema>;

interface ProviderFormProps {
  onSubmit: (data: CreateProviderRequest) => void;
  isLoading?: boolean;
}

export function ProviderForm({ onSubmit, isLoading }: ProviderFormProps) {
  const form = useForm<ProviderFormData>({
    resolver: zodResolver(providerSchema),
    defaultValues: {
      name: '',
      displayName: '',
      authority: '',
      clientId: '',
      clientSecret: '',
      scopes: 'openid profile email',
      jitProvisioningEnabled: true,
    }
  });

  const handleFormSubmit = (data: ProviderFormData) => {
    const request: CreateProviderRequest = {
      name: data.name,
      displayName: data.displayName,
      authority: data.authority,
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
        <FormField
          control={form.control}
          name="name"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Name</FormLabel>
              <FormControl>
                <Input {...field} placeholder="google" />
              </FormControl>
              <FormDescription>Unique identifier (lowercase, hyphens allowed)</FormDescription>
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
                <Input {...field} placeholder="Google" />
              </FormControl>
              <FormDescription>Name shown to users on the login page</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="authority"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Authority</FormLabel>
              <FormControl>
                <Input {...field} placeholder="https://accounts.google.com" />
              </FormControl>
              <FormDescription>OIDC issuer URL</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="clientId"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Client ID</FormLabel>
              <FormControl>
                <Input {...field} placeholder="your-client-id" />
              </FormControl>
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
                <Input {...field} type="password" placeholder="your-client-secret" />
              </FormControl>
              <FormDescription>Optional for public clients</FormDescription>
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
              <FormDescription>Space-separated list of OIDC scopes</FormDescription>
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
                <FormLabel>
                  Just-In-Time Provisioning
                </FormLabel>
                <p className="text-sm text-muted-foreground">
                  Automatically create user accounts on first login
                </p>
              </div>
            </FormItem>
          )}
        />

        <div className="flex gap-2">
          <Button type="submit" disabled={isLoading}>
            {isLoading ? 'Creating...' : 'Create Provider'}
          </Button>
          <Button type="button" variant="outline" onClick={() => window.history.back()}>
            Cancel
          </Button>
        </div>
      </form>
    </Form>
  );
}
