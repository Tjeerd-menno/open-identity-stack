/**
 * UserForm Component
 * 
 * Form component for creating and editing users
 */

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
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
import type { ApiError, CreateUserRequest, UpdateUserRequest, User } from '@/types';

const createUserSchema = z.object({
  email: z.string().email('Invalid email address'),
  displayName: z.string().min(1, 'Display name is required').max(100),
  password: z
    .string()
    .min(8, 'Password must be at least 8 characters')
    .regex(/[A-Z]/, 'Password must contain uppercase letter')
    .regex(/[a-z]/, 'Password must contain lowercase letter')
    .regex(/[0-9]/, 'Password must contain number')
    .regex(/[^A-Za-z0-9]/, 'Password must contain special character'),
});

const updateUserSchema = z.object({
  displayName: z.string().min(1, 'Display name is required').max(100),
});

type CreateUserFormData = z.infer<typeof createUserSchema>;
type UpdateUserFormData = z.infer<typeof updateUserSchema>;

type UserFormProps = {
  onCancel?: () => void;
  isSubmitting?: boolean;
} & (
  | {
      user?: undefined;
      onSubmit: (data: CreateUserRequest) => Promise<void>;
    }
  | {
      user: User;
      onSubmit: (data: UpdateUserRequest) => Promise<void>;
    }
);

function getErrorMessage(error: unknown): string {
  if (!error || typeof error !== 'object') {
    return 'The request could not be completed.';
  }

  const apiError = error as ApiError;
  return apiError.detail || apiError.title || 'The request could not be completed.';
}

export function UserForm({ user, onSubmit, onCancel, isSubmitting }: UserFormProps) {
  const isEditMode = !!user;

  const form = useForm<CreateUserFormData | UpdateUserFormData>({
    resolver: zodResolver(isEditMode ? updateUserSchema : createUserSchema),
    defaultValues: isEditMode
      ? {
          displayName: user.displayName,
        }
      : {
          email: '',
          displayName: '',
          password: '',
        },
  });

  const handleSubmit = async (data: CreateUserFormData | UpdateUserFormData) => {
    form.clearErrors('root');

    try {
      if (isEditMode) {
        await onSubmit(data as UpdateUserRequest);
      } else {
        await onSubmit(data as CreateUserRequest);
      }
    } catch (error) {
      form.setError('root', {
        message: getErrorMessage(error),
      });
    }
  };

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(handleSubmit)} className="space-y-6">
        {!isEditMode && (
          <FormField
            control={form.control}
            name="email"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Email</FormLabel>
                <FormControl>
                  <Input
                    type="email"
                    placeholder="user@example.com"
                    {...field}
                  />
                </FormControl>
                <FormDescription>
                  The user's email address for login
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
                <Input placeholder="John Doe" {...field} />
              </FormControl>
              <FormDescription>
                The name shown in the UI
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        {!isEditMode && (
          <FormField
            control={form.control}
            name="password"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Password</FormLabel>
                <FormControl>
                  <Input type="password" placeholder="••••••••" {...field} />
                </FormControl>
                <FormDescription>
                  Must be at least 8 characters with uppercase, lowercase, number, and special character
                </FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />
        )}

        {form.formState.errors.root?.message ? (
          <div className="text-sm font-medium text-destructive">
            {form.formState.errors.root.message}
          </div>
        ) : null}

        <div className="flex gap-4">
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting 
              ? (isEditMode ? 'Updating...' : 'Creating...') 
              : (isEditMode ? 'Update User' : 'Create User')
            }
          </Button>
          {onCancel && (
            <Button type="button" variant="outline" onClick={onCancel}>
              Cancel
            </Button>
          )}
        </div>
      </form>
    </Form>
  );
}
