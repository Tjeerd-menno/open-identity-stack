import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import type { RegisterServicePermissionInput, RegisterServiceRequest } from '@/types';
import { Plus, Trash2 } from 'lucide-react';

interface RegisterServiceFormProps {
  onSubmit: (request: RegisterServiceRequest) => Promise<void>;
  isLoading?: boolean;
}

const emptyPermission: RegisterServicePermissionInput = {
  permissionKey: '',
  displayName: '',
  description: '',
  intendedUse: '',
  documentationUrl: '',
};

export function RegisterServiceForm({ onSubmit, isLoading = false }: RegisterServiceFormProps) {
  const [serviceIdentifier, setServiceIdentifier] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [description, setDescription] = useState('');
  const [ownerId, setOwnerId] = useState('');
  const [ownerType, setOwnerType] = useState('User');
  const [permissions, setPermissions] = useState<RegisterServicePermissionInput[]>([{ ...emptyPermission }]);

  const updatePermission = (index: number, patch: Partial<RegisterServicePermissionInput>) => {
    setPermissions((current) => current.map((permission, currentIndex) =>
      currentIndex === index ? { ...permission, ...patch } : permission
    ));
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    await onSubmit({
      serviceIdentifier,
      displayName,
      description,
      ownerId,
      ownerType,
      permissions,
    });
  };

  return (
    <form className="space-y-6" onSubmit={submit}>
      <Card>
        <CardHeader>
          <CardTitle>Service</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="serviceIdentifier">Service Identifier</Label>
            <Input id="serviceIdentifier" value={serviceIdentifier} onChange={(event) => setServiceIdentifier(event.target.value)} placeholder="inventory-api" required />
          </div>
          <div className="space-y-2">
            <Label htmlFor="displayName">Display Name</Label>
            <Input id="displayName" value={displayName} onChange={(event) => setDisplayName(event.target.value)} placeholder="Inventory API" required />
          </div>
          <div className="space-y-2">
            <Label htmlFor="ownerId">Owner ID</Label>
            <Input id="ownerId" value={ownerId} onChange={(event) => setOwnerId(event.target.value)} required />
          </div>
          <div className="space-y-2">
            <Label htmlFor="ownerType">Owner Type</Label>
            <Input id="ownerType" value={ownerType} onChange={(event) => setOwnerType(event.target.value)} placeholder="User, Group, or ExternalGroup" required />
          </div>
          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="description">Description</Label>
            <Textarea id="description" value={description} onChange={(event) => setDescription(event.target.value)} />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Initial Permissions</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {permissions.map((permission, index) => (
            <div key={index} className="grid gap-3 rounded-md border p-4 md:grid-cols-2">
              <Input aria-label={`Permission key ${index + 1}`} value={permission.permissionKey} onChange={(event) => updatePermission(index, { permissionKey: event.target.value })} placeholder="read" required />
              <Input aria-label={`Permission display name ${index + 1}`} value={permission.displayName} onChange={(event) => updatePermission(index, { displayName: event.target.value })} placeholder="Read inventory" required />
              <Textarea value={permission.description ?? ''} onChange={(event) => updatePermission(index, { description: event.target.value })} placeholder="Description" />
              <Textarea value={permission.intendedUse ?? ''} onChange={(event) => updatePermission(index, { intendedUse: event.target.value })} placeholder="Intended use" />
              <Input className="md:col-span-2" value={permission.documentationUrl ?? ''} onChange={(event) => updatePermission(index, { documentationUrl: event.target.value })} placeholder="Documentation URL" />
              <Button type="button" variant="outline" onClick={() => setPermissions((current) => current.filter((_, currentIndex) => currentIndex !== index))} disabled={permissions.length === 1}>
                <Trash2 className="mr-2 h-4 w-4" />
                Remove
              </Button>
            </div>
          ))}
          <Button type="button" variant="outline" onClick={() => setPermissions((current) => [...current, { ...emptyPermission }])}>
            <Plus className="mr-2 h-4 w-4" />
            Add Permission
          </Button>
        </CardContent>
      </Card>

      <div className="flex justify-end">
        <Button type="submit" disabled={isLoading}>{isLoading ? 'Registering...' : 'Register Service'}</Button>
      </div>
    </form>
  );
}
