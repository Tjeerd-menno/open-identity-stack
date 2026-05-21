import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { ServicePermissionStatusBadge } from './ServicePermissionStatusBadge';
import {
  useAddServicePermission,
  useChangePermissionLifecycle,
  useChangeServiceLifecycle,
  useMaintainerMutations,
  useRegisteredService,
  useTransferServiceOwnership,
  useUpdateRegisteredService,
} from '../hooks';

export function RegisteredServiceDetail({ id }: { id: string }) {
  const { data: service, isLoading } = useRegisteredService(id);
  const updateService = useUpdateRegisteredService(id);
  const addPermission = useAddServicePermission(id);
  const changeServiceLifecycle = useChangeServiceLifecycle(id);
  const transferOwnership = useTransferServiceOwnership(id);
  const maintainers = useMaintainerMutations(id);
  const [permissionDraft, setPermissionDraft] = useState({ permissionKey: '', displayName: '' });
  const [ownerDraft, setOwnerDraft] = useState({ ownerId: '', ownerType: 'User' });
  const [maintainerDraft, setMaintainerDraft] = useState({ principalId: '', principalType: 'User' });

  if (isLoading) {
    return <div>Loading service...</div>;
  }

  if (!service) {
    return <div>Service not found.</div>;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-3xl font-bold">{service.displayName}</h1>
          <p className="text-muted-foreground">{service.serviceIdentifier}</p>
        </div>
        <ServicePermissionStatusBadge status={service.status} />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Service Metadata</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="displayName">Display Name</Label>
              <Input id="displayName" defaultValue={service.displayName} onBlur={(event) => {
                if (event.target.value !== service.displayName) {
                  updateService.mutate({ displayName: event.target.value, description: service.description, concurrencyToken: service.concurrencyToken });
                }
              }} />
            </div>
            <div className="space-y-2">
              <Label>Owner</Label>
              <div className="text-sm">{service.ownerType}: {service.ownerId}</div>
            </div>
          </div>
          <Textarea defaultValue={service.description ?? ''} onBlur={(event) => {
            if (event.target.value !== (service.description ?? '')) {
              updateService.mutate({ displayName: service.displayName, description: event.target.value, concurrencyToken: service.concurrencyToken });
            }
          }} />
          <div className="flex gap-2">
            {['Active', 'Disabled', 'Retired'].map((status) => (
              <Button key={status} type="button" variant="outline" onClick={() => changeServiceLifecycle.mutate({ status, acknowledgeDependencies: true, concurrencyToken: service.concurrencyToken })}>
                Mark {status}
              </Button>
            ))}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Permissions</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-2 md:grid-cols-[1fr_1fr_auto]">
            <Input placeholder="permission-key" value={permissionDraft.permissionKey} onChange={(event) => setPermissionDraft({ ...permissionDraft, permissionKey: event.target.value })} />
            <Input placeholder="Display name" value={permissionDraft.displayName} onChange={(event) => setPermissionDraft({ ...permissionDraft, displayName: event.target.value })} />
            <Button type="button" onClick={() => addPermission.mutate({ ...permissionDraft, concurrencyToken: service.concurrencyToken })}>Add</Button>
          </div>
          <div className="space-y-2">
            {service.permissions.map((permission) => (
              <div key={permission.id} className="flex items-center justify-between rounded-md border p-3">
                <div>
                  <div className="font-medium">{permission.fullPermissionKey}</div>
                  <div className="text-sm text-muted-foreground">{permission.displayName}</div>
                </div>
                <div className="flex items-center gap-2">
                  <ServicePermissionStatusBadge status={permission.status} />
                  <PermissionLifecycleButton permissionId={permission.id} concurrencyToken={service.concurrencyToken} />
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Ownership And Maintainers</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-2 md:grid-cols-[1fr_1fr_auto]">
            <Input placeholder="New owner ID" value={ownerDraft.ownerId} onChange={(event) => setOwnerDraft({ ...ownerDraft, ownerId: event.target.value })} />
            <Input placeholder="Owner type" value={ownerDraft.ownerType} onChange={(event) => setOwnerDraft({ ...ownerDraft, ownerType: event.target.value })} />
            <Button type="button" onClick={() => transferOwnership.mutate({ ...ownerDraft, concurrencyToken: service.concurrencyToken })}>Transfer</Button>
          </div>
          <div className="grid gap-2 md:grid-cols-[1fr_1fr_auto]">
            <Input placeholder="Maintainer principal ID" value={maintainerDraft.principalId} onChange={(event) => setMaintainerDraft({ ...maintainerDraft, principalId: event.target.value })} />
            <Input placeholder="Principal type" value={maintainerDraft.principalType} onChange={(event) => setMaintainerDraft({ ...maintainerDraft, principalType: event.target.value })} />
            <Button type="button" onClick={() => maintainers.add.mutate({ ...maintainerDraft, concurrencyToken: service.concurrencyToken })}>Add</Button>
          </div>
          {service.maintainers.map((maintainer) => (
            <div key={maintainer.id} className="flex items-center justify-between rounded-md border p-3">
              <span>{maintainer.principalType}: {maintainer.principalId}</span>
              <Button variant="outline" type="button" onClick={() => maintainers.remove.mutate({ principalId: maintainer.principalId, concurrencyToken: service.concurrencyToken })}>Remove</Button>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}

function PermissionLifecycleButton({ permissionId, concurrencyToken }: { permissionId: string; concurrencyToken: number }) {
  const mutation = useChangePermissionLifecycle(permissionId);
  return (
    <Button
      type="button"
      variant="outline"
      onClick={() => mutation.mutate({ status: 'Deprecated', acknowledgeDependencies: true, concurrencyToken })}
    >
      Deprecate
    </Button>
  );
}
