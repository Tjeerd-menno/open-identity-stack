import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useAssignRole } from '../hooks/useAssignRole';
import { useRoles } from '../../roles/hooks/useRoles';
import { usePermission } from '@/features/auth/hooks/useAuth';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { Alert, AlertDescription } from '@/components/ui/alert';
import type { RoleListItem } from '@/types';

const assignRoleSchema = z.object({
  roleId: z.string().min(1, 'Role is required'),
});

type AssignRoleFormData = z.infer<typeof assignRoleSchema>;

interface AssignRoleDialogProps {
  userId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess?: () => void;
}

export function AssignRoleDialog({
  userId,
  open,
  onOpenChange,
  onSuccess,
}: AssignRoleDialogProps) {
  const hasRolesRead = usePermission('roles:read');
  const assignRole = useAssignRole();
  // Fetch all roles to populate the select - only if we have roles:read permission
  const { data: rolesData, isLoading: isLoadingRoles } = useRoles({ 
    page: 1, 
    pageSize: 100,
    enabled: open && hasRolesRead,
  });
  const [isSubmitting, setIsSubmitting] = useState(false);

  const form = useForm<AssignRoleFormData>({
    resolver: zodResolver(assignRoleSchema),
    defaultValues: {
      roleId: '',
    },
  });

  const onSubmit = async (data: AssignRoleFormData) => {
    setIsSubmitting(true);
    try {
      await assignRole.mutateAsync({ userId, roleId: data.roleId });
      form.reset();
      onOpenChange(false);
      onSuccess?.();
    } catch (error) {
      console.error('Failed to assign role:', error);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle>Assign Role</DialogTitle>
          <DialogDescription>
            Select a role to assign to this user.
          </DialogDescription>
        </DialogHeader>
        
        {!hasRolesRead ? (
          <Alert>
            <AlertDescription>
              You do not have permission to view available roles. Contact your administrator to assign roles.
            </AlertDescription>
          </Alert>
        ) : (
          <Form {...form}>
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
              <FormField
                control={form.control}
                name="roleId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Role</FormLabel>
                    <Select onValueChange={field.onChange} defaultValue={field.value}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Select a role" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {isLoadingRoles ? (
                          <div className="flex justify-center p-2">
                            <LoadingSpinner size="sm" />
                          </div>
                        ) : (
                          rolesData?.items.map((role: RoleListItem) => (
                            <SelectItem key={role.id} value={role.id}>
                              {role.displayName}
                            </SelectItem>
                          ))
                        )}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
              
              <DialogFooter>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => onOpenChange(false)}
                  disabled={isSubmitting}
                >
                  Cancel
                </Button>
                <Button type="submit" disabled={isSubmitting}>
                  {isSubmitting ? 'Assigning...' : 'Assign Role'}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        )}
      </DialogContent>
    </Dialog>
  );
}
