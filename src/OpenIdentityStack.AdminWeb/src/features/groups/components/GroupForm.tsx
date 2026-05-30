/**
 * GroupForm Component
 * 
 * Form component for creating and editing groups
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
import { Textarea } from '@/components/ui/textarea';
import type { Group } from '@/types';

const createGroupSchema = z.object({
  name: z.string().min(1, 'Group name is required').max(256),
  description: z.string().max(1024).optional(),
});

const updateGroupSchema = z.object({
  name: z.string().min(1, 'Group name is required').max(256),
  description: z.string().max(1024).optional(),
});

type CreateGroupFormData = z.infer<typeof createGroupSchema>;
type UpdateGroupFormData = z.infer<typeof updateGroupSchema>;

interface GroupFormProps {
  group?: Group;
  onSubmit: (data: CreateGroupFormData | UpdateGroupFormData) => Promise<void>;
  onCancel?: () => void;
  isSubmitting?: boolean;
}

export function GroupForm({ group, onSubmit, onCancel, isSubmitting }: GroupFormProps) {
  const isEditMode = !!group;

  const form = useForm<CreateGroupFormData | UpdateGroupFormData>({
    resolver: zodResolver(isEditMode ? updateGroupSchema : createGroupSchema),
    defaultValues: isEditMode
      ? {
          name: group.name,
          description: group.description || '',
        }
      : {
          name: '',
          description: '',
        },
  });

  const handleSubmit = async (data: CreateGroupFormData | UpdateGroupFormData) => {
    await onSubmit(data);
  };

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(handleSubmit)} className="space-y-6">
        <FormField
          control={form.control}
          name="name"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Group Name</FormLabel>
              <FormControl>
                <Input placeholder="e.g., Developers, Admins" {...field} />
              </FormControl>
              <FormDescription>
                A unique name for this group
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="description"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Description</FormLabel>
              <FormControl>
                <Textarea
                  placeholder="Optional description of this group's purpose"
                  {...field}
                  rows={4}
                />
              </FormControl>
              <FormDescription>
                Optional description to help identify the group's purpose
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <div className="flex gap-4">
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting 
              ? (isEditMode ? 'Updating...' : 'Creating...') 
              : (isEditMode ? 'Update Group' : 'Create Group')
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
