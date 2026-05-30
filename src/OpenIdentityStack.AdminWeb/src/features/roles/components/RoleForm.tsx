import { useEffect } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { PermissionSelector } from './PermissionSelector';
import type { Role, CreateRoleRequest } from '@/types';

const roleFormSchema = z.object({
  name: z
    .string()
    .min(3, 'Name must be at least 3 characters')
    .max(50, 'Name must be less than 50 characters')
    .regex(
      /^[a-z0-9-]+$/,
      'Name must contain only lowercase letters, numbers, and hyphens'
    ),
  displayName: z
    .string()
    .min(3, 'Display name must be at least 3 characters')
    .max(100, 'Display name must be less than 100 characters'),
  description: z
    .string()
    .max(500, 'Description must be less than 500 characters')
    .optional(),
  permissions: z
    .array(z.string())
    .min(1, 'At least one permission is required'),
  acknowledgeWildcardGrant: z.boolean().optional(),
});

type RoleFormData = z.infer<typeof roleFormSchema>;

interface RoleFormProps {
  /**
   * Existing role to edit (undefined for create mode)
   */
  role?: Role;
  
  /**
   * Callback when form is submitted
   */
  onSubmit: (data: CreateRoleRequest) => void;
  
  /**
   * Callback when form is cancelled
   */
  onCancel: () => void;
  
  /**
   * Whether the form is in a loading state
   */
  isLoading?: boolean;
}

/**
 * Form component for creating and editing roles
 * 
 * Handles validation, permission selection, and submission.
 * In edit mode, the name field is disabled (roles cannot be renamed).
 */
export function RoleForm({ role, onSubmit, onCancel, isLoading = false }: RoleFormProps) {
  const isEditMode = !!role;

  const form = useForm<RoleFormData>({
    resolver: zodResolver(roleFormSchema),
    defaultValues: {
      name: role?.name || '',
      displayName: role?.displayName || '',
      description: role?.description || '',
      permissions: role?.permissions || [],
      acknowledgeWildcardGrant: false,
    },
  });

  // Reset form when role changes (e.g., switching between different roles)
  useEffect(() => {
    if (role) {
      form.reset({
        name: role.name,
        displayName: role.displayName,
        description: role.description || '',
        permissions: role.permissions,
        acknowledgeWildcardGrant: false,
      });
    }
  }, [role, form]);

  const handleSubmit = (data: RoleFormData) => {
    if (isEditMode) {
      // In edit mode, send all fields (for compatibility with onSubmit signature)
      const updateData: CreateRoleRequest = {
        name: data.name,
        displayName: data.displayName,
        description: data.description,
        permissions: data.permissions,
        ...(data.acknowledgeWildcardGrant ? { acknowledgeWildcardGrant: true } : {}),
      };
      onSubmit(updateData);
    } else {
      // In create mode, send all fields
      const createData: CreateRoleRequest = {
        name: data.name,
        displayName: data.displayName,
        description: data.description,
        permissions: data.permissions,
        ...(data.acknowledgeWildcardGrant ? { acknowledgeWildcardGrant: true } : {}),
      };
      onSubmit(createData);
    }
  };

  const selectedPermissions = useWatch({ control: form.control, name: 'permissions' }) ?? [];
  const hasWildcardGrant = selectedPermissions.some((permission) => permission === '*' || permission.endsWith(':*'));

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(handleSubmit)} className="space-y-6">
        {/* Name field - disabled in edit mode */}
        <FormField
          control={form.control}
          name="name"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Name</FormLabel>
              <FormControl>
                <Input
                  placeholder="custom-admin"
                  {...field}
                  disabled={isEditMode || isLoading}
                />
              </FormControl>
              <FormDescription>
                Unique role identifier (lowercase, numbers, and hyphens only)
                {isEditMode && ' - Cannot be changed after creation'}
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        {/* Display Name field */}
        <FormField
          control={form.control}
          name="displayName"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Display Name</FormLabel>
              <FormControl>
                <Input
                  placeholder="Custom Admin"
                  {...field}
                  disabled={isLoading}
                />
              </FormControl>
              <FormDescription>
                Human-readable name shown in the UI
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        {/* Description field */}
        <FormField
          control={form.control}
          name="description"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Description (Optional)</FormLabel>
              <FormControl>
                <Textarea
                  placeholder="Describe the purpose of this role..."
                  rows={3}
                  {...field}
                  disabled={isLoading}
                />
              </FormControl>
              <FormDescription>
                Optional description of the role's purpose
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        {/* Permissions field */}
        <FormField
          control={form.control}
          name="permissions"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Permissions</FormLabel>
              <FormControl>
                <PermissionSelector
                  selectedPermissions={field.value}
                  onChange={field.onChange}
                  disabled={isLoading}
                />
              </FormControl>
              <FormDescription>
                Select the permissions granted to users with this role
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        {hasWildcardGrant && (
          <FormField
            control={form.control}
            name="acknowledgeWildcardGrant"
            render={({ field }) => (
              <FormItem className="rounded-md border p-4">
                <div className="flex items-start gap-3">
                  <FormControl>
                    <Checkbox
                      checked={field.value ?? false}
                      onCheckedChange={field.onChange}
                      disabled={isLoading}
                      aria-label="Acknowledge wildcard grant"
                    />
                  </FormControl>
                  <div className="space-y-1">
                    <FormLabel>Acknowledge wildcard grant</FormLabel>
                    <FormDescription>
                      This role includes a broad wildcard permission grant.
                    </FormDescription>
                  </div>
                </div>
                <FormMessage />
              </FormItem>
            )}
          />
        )}

        {/* Action buttons */}
        <div className="flex justify-end space-x-4">
          <Button
            type="button"
            variant="outline"
            onClick={onCancel}
            disabled={isLoading}
          >
            Cancel
          </Button>
          <Button type="submit" disabled={isLoading}>
            {isLoading
              ? isEditMode
                ? 'Updating...'
                : 'Creating...'
              : isEditMode
              ? 'Update Role'
              : 'Create Role'}
          </Button>
        </div>
      </form>
    </Form>
  );
}
