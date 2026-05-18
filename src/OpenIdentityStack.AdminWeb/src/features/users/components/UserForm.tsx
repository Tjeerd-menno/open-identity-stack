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
import type { ApiError, CreateUserRequest, UpdateUserRequest, User, UserProfileRequest } from '@/types';

const optionalText = (maxLength: number) =>
  z.string().max(maxLength).optional();

const optionalUrl = z
  .string()
  .max(2048)
  .refine((value) => {
    if (!value?.trim()) {
      return true;
    }

    try {
      const url = new URL(value);
      return url.protocol === 'https:' || url.protocol === 'http:';
    } catch {
      return false;
    }
  }, 'Enter a valid http or https URL')
  .optional();

const profileFieldsSchema = {
  givenName: optionalText(256),
  familyName: optionalText(256),
  middleName: optionalText(256),
  nickname: optionalText(256),
  preferredUsername: z
    .string()
    .max(64)
    .regex(/^[A-Za-z0-9._-]*$/, 'Use only letters, numbers, dots, underscores, and hyphens')
    .optional(),
  profileUrl: optionalUrl,
  pictureUrl: optionalUrl,
  websiteUrl: optionalUrl,
  gender: optionalText(64),
  birthdate: optionalText(32),
  zoneInfo: optionalText(128),
  locale: optionalText(35),
};

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
  ...profileFieldsSchema,
});

const updateUserSchema = z.object({
  displayName: z.string().min(1, 'Display name is required').max(100),
  ...profileFieldsSchema,
});

type CreateUserFormData = z.infer<typeof createUserSchema>;
type UpdateUserFormData = z.infer<typeof updateUserSchema>;
type UserFormData = CreateUserFormData | UpdateUserFormData;

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

function toNullableValue(value: string | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

function toProfileRequest(data: UserFormData): UserProfileRequest {
  return {
    givenName: toNullableValue(data.givenName),
    familyName: toNullableValue(data.familyName),
    middleName: toNullableValue(data.middleName),
    nickname: toNullableValue(data.nickname),
    preferredUsername: toNullableValue(data.preferredUsername),
    profile: toNullableValue(data.profileUrl),
    picture: toNullableValue(data.pictureUrl),
    website: toNullableValue(data.websiteUrl),
    gender: toNullableValue(data.gender),
    birthdate: toNullableValue(data.birthdate),
    zoneInfo: toNullableValue(data.zoneInfo),
    locale: toNullableValue(data.locale),
  };
}

export function UserForm({ user, onSubmit, onCancel, isSubmitting }: UserFormProps) {
  const isEditMode = !!user;

  const form = useForm<UserFormData>({
    resolver: zodResolver(isEditMode ? updateUserSchema : createUserSchema),
    defaultValues: isEditMode
      ? {
          displayName: user.displayName,
          givenName: user.profile?.givenName ?? '',
          familyName: user.profile?.familyName ?? '',
          middleName: user.profile?.middleName ?? '',
          nickname: user.profile?.nickname ?? '',
          preferredUsername: user.profile?.preferredUsername ?? '',
          profileUrl: user.profile?.profile ?? '',
          pictureUrl: user.profile?.picture ?? '',
          websiteUrl: user.profile?.website ?? '',
          gender: user.profile?.gender ?? '',
          birthdate: user.profile?.birthdate ?? '',
          zoneInfo: user.profile?.zoneInfo ?? '',
          locale: user.profile?.locale ?? '',
        }
      : {
          email: '',
          displayName: '',
          password: '',
          givenName: '',
          familyName: '',
          middleName: '',
          nickname: '',
          preferredUsername: '',
          profileUrl: '',
          pictureUrl: '',
          websiteUrl: '',
          gender: '',
          birthdate: '',
          zoneInfo: '',
          locale: '',
        },
  });

  const handleSubmit = async (data: UserFormData) => {
    form.clearErrors('root');

    try {
      const profile = toProfileRequest(data);

      if (isEditMode) {
        await onSubmit({
          displayName: data.displayName,
          profile,
        });
      } else {
        const createData = data as CreateUserFormData;
        await onSubmit({
          email: createData.email,
          displayName: createData.displayName,
          password: createData.password,
          profile,
        });
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

        <div className="space-y-4 border-t pt-6">
          <div>
            <h2 className="text-lg font-semibold">OIDC Profile</h2>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <FormField
              control={form.control}
              name="givenName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Given Name</FormLabel>
                  <FormControl>
                    <Input placeholder="John" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="familyName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Family Name</FormLabel>
                  <FormControl>
                    <Input placeholder="Doe" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="middleName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Middle Name</FormLabel>
                  <FormControl>
                    <Input placeholder="Quincy" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="nickname"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Nickname</FormLabel>
                  <FormControl>
                    <Input placeholder="johnny" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="preferredUsername"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Preferred Username</FormLabel>
                  <FormControl>
                    <Input placeholder="john.doe" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="locale"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Locale</FormLabel>
                  <FormControl>
                    <Input placeholder="en-US" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="zoneInfo"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Time Zone</FormLabel>
                  <FormControl>
                    <Input placeholder="Europe/Amsterdam" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="birthdate"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Birthdate</FormLabel>
                  <FormControl>
                    <Input placeholder="1990-01-31" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="gender"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Gender</FormLabel>
                  <FormControl>
                    <Input placeholder="female" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <FormField
            control={form.control}
            name="profileUrl"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Profile URL</FormLabel>
                <FormControl>
                  <Input placeholder="https://example.com/profiles/john.doe" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="pictureUrl"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Picture URL</FormLabel>
                <FormControl>
                  <Input placeholder="https://example.com/profiles/john.doe/avatar.png" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="websiteUrl"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Website URL</FormLabel>
                <FormControl>
                  <Input placeholder="https://example.com" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
        </div>

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
