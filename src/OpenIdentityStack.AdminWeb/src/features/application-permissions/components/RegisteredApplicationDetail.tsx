import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { ApplicationPermissionStatusBadge } from './ApplicationPermissionStatusBadge';
import {
  useAddApplicationPermission,
  useRegisteredApplication,
} from '../hooks';

export function RegisteredApplicationDetail({ id }: { id: string }) {
  const { data: application, isLoading } = useRegisteredApplication(id);
  const addPermission = useAddApplicationPermission(id);
  const [permissionDraft, setPermissionDraft] = useState({ name: '', description: '', category: '' });

  if (isLoading) {
    return <div>Loading application...</div>;
  }

  if (!application) {
    return <div>Application not found.</div>;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-3xl font-bold">{application.displayName}</h1>
          <p className="text-muted-foreground">{application.applicationIdentifier}</p>
        </div>
        <ApplicationPermissionStatusBadge status={application.status} />
      </div>

      {application.description && (
        <section className="rounded-md border p-4">
          <Label>Version</Label>
          <div className="mt-1 text-sm">{application.description}</div>
        </section>
      )}

      <section className="space-y-4 rounded-md border p-4">
        <h2 className="text-lg font-semibold">Permissions</h2>
        <div className="grid gap-3 md:grid-cols-[1fr_1fr_auto]">
          <Input
            aria-label="Permission name"
            placeholder="read:patients"
            value={permissionDraft.name}
            onChange={(event) => setPermissionDraft({ ...permissionDraft, name: event.target.value })}
          />
          <Input
            aria-label="Permission category"
            placeholder="Patients"
            value={permissionDraft.category}
            onChange={(event) => setPermissionDraft({ ...permissionDraft, category: event.target.value })}
          />
          <Button
            type="button"
            onClick={() => addPermission.mutate({
              permissionKey: permissionDraft.name,
              displayName: permissionDraft.name,
              description: permissionDraft.description,
              intendedUse: permissionDraft.category,
              documentationUrl: null,
              concurrencyToken: application.concurrencyToken,
            })}
          >
            Add
          </Button>
          <Textarea
            className="md:col-span-3"
            aria-label="Permission description"
            placeholder="Allows reading patient data"
            value={permissionDraft.description}
            onChange={(event) => setPermissionDraft({ ...permissionDraft, description: event.target.value })}
          />
        </div>
          <div className="space-y-2">
            {application.permissions.map((permission) => (
              <div key={permission.id} className="flex items-center justify-between rounded-md border p-3">
                <div>
                  <div className="font-medium">{permission.fullPermissionKey}</div>
                  {permission.description && (
                    <div className="text-sm text-muted-foreground">{permission.description}</div>
                  )}
                  {permission.intendedUse && (
                    <div className="mt-1 text-xs text-muted-foreground">{permission.intendedUse}</div>
                  )}
                </div>
                <ApplicationPermissionStatusBadge status={permission.status} />
              </div>
            ))}
          </div>
      </section>
    </div>
  );
}
