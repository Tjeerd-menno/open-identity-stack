import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import type { PermissionManifestPermission, PermissionManifestRequest } from '@/types';
import { Download, Plus, Trash2 } from 'lucide-react';

interface RegisterApplicationFormProps {
  onSubmit: (request: PermissionManifestRequest) => Promise<void>;
  onImportEndpoint?: (endpoint: string) => Promise<void>;
  isLoading?: boolean;
  isImporting?: boolean;
}

const emptyPermission: PermissionManifestPermission = {
  key: '',
  displayName: '',
  description: '',
  category: '',
};

export function RegisterApplicationForm({
  onSubmit,
  onImportEndpoint,
  isLoading = false,
  isImporting = false,
}: RegisterApplicationFormProps) {
  const [applicationId, setApplicationId] = useState('');
  const [applicationDisplayName, setApplicationDisplayName] = useState('');
  const [applicationDescription, setApplicationDescription] = useState('');
  const [applicationVersion, setApplicationVersion] = useState('');
  const [ownerId, setOwnerId] = useState('');
  const [ownerType, setOwnerType] = useState<'user' | 'group'>('user');
  const [endpoint, setEndpoint] = useState('');
  const [permissions, setPermissions] = useState<PermissionManifestPermission[]>([{ ...emptyPermission }]);
  const [validationError, setValidationError] = useState<string | null>(null);

  const updatePermission = (index: number, patch: Partial<PermissionManifestPermission>) => {
    setPermissions((current) => current.map((permission, currentIndex) =>
      currentIndex === index ? { ...permission, ...patch } : permission
    ));
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const keys = permissions.map((permission) => permission.key.trim()).filter(Boolean);
    if (new Set(keys).size !== keys.length) {
      setValidationError('Permission keys must be unique.');
      return;
    }

    setValidationError(null);
    await onSubmit({
      manifest: {
        schemaVersion: '1.0.0',
        application: {
          id: applicationId,
          displayName: applicationDisplayName,
          description: applicationDescription || null,
          version: applicationVersion,
        },
        permissions: permissions.map((permission) => ({
          key: permission.key,
          displayName: permission.displayName,
          description: permission.description || null,
          category: permission.category || null,
        })),
      },
      ownerId,
      ownerType,
      manifestBaseUrl: null,
    });
  };

  const importEndpoint = async (event: FormEvent) => {
    event.preventDefault();
    if (onImportEndpoint) {
      await onImportEndpoint(endpoint);
    }
  };

  return (
    <div className="space-y-8">
      <form className="space-y-4 rounded-md border p-4" onSubmit={importEndpoint}>
        <div className="grid gap-3 md:grid-cols-[1fr_auto] md:items-end">
          <div className="space-y-2">
            <Label htmlFor="permissionEndpoint">Well-known permissions endpoint</Label>
            <Input
              id="permissionEndpoint"
              value={endpoint}
              onChange={(event) => setEndpoint(event.target.value)}
              placeholder="https://patient.example/.well-known/permissions"
              type="url"
            />
          </div>
          <Button type="submit" variant="outline" disabled={!endpoint || isImporting || !onImportEndpoint}>
            <Download className="mr-2 h-4 w-4" />
            {isImporting ? 'Importing...' : 'Import Endpoint'}
          </Button>
        </div>
      </form>

      <form className="space-y-6" onSubmit={submit}>
        <section className="space-y-4 rounded-md border p-4">
          <h2 className="text-lg font-semibold">Application</h2>
          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <Label htmlFor="applicationId">Application ID</Label>
              <Input id="applicationId" value={applicationId} onChange={(event) => setApplicationId(event.target.value)} placeholder="patient-api" required />
            </div>
            <div className="space-y-2">
              <Label htmlFor="applicationDisplayName">Display Name</Label>
              <Input id="applicationDisplayName" value={applicationDisplayName} onChange={(event) => setApplicationDisplayName(event.target.value)} placeholder="Patient API" required />
            </div>
            <div className="space-y-2">
              <Label htmlFor="applicationVersion">Version</Label>
              <Input id="applicationVersion" value={applicationVersion} onChange={(event) => setApplicationVersion(event.target.value)} placeholder="1.0.0" required />
            </div>
            <div className="space-y-2 md:col-span-3">
              <Label htmlFor="applicationDescription">Description</Label>
              <Textarea id="applicationDescription" value={applicationDescription} onChange={(event) => setApplicationDescription(event.target.value)} placeholder="Patient records" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="ownerId">Owner ID</Label>
              <Input id="ownerId" value={ownerId} onChange={(event) => setOwnerId(event.target.value)} placeholder="owner-1" required />
            </div>
            <div className="space-y-2">
              <Label htmlFor="ownerType">Owner Type</Label>
              <select
                id="ownerType"
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={ownerType}
                onChange={(event) => setOwnerType(event.target.value as 'user' | 'group')}
              >
                <option value="user">User</option>
                <option value="group">Group</option>
              </select>
            </div>
          </div>
        </section>

        <section className="space-y-4 rounded-md border p-4">
          <h2 className="text-lg font-semibold">Permissions</h2>
          {permissions.map((permission, index) => (
            <div key={index} className="grid gap-3 rounded-md border p-4 md:grid-cols-2">
              <Input aria-label={`Permission key ${index + 1}`} value={permission.key} onChange={(event) => updatePermission(index, { key: event.target.value })} placeholder="patient:read" required />
              <Input aria-label={`Permission display name ${index + 1}`} value={permission.displayName} onChange={(event) => updatePermission(index, { displayName: event.target.value })} placeholder="Read patients" required />
              <Input aria-label={`Permission category ${index + 1}`} value={permission.category ?? ''} onChange={(event) => updatePermission(index, { category: event.target.value })} placeholder="Patients" />
              <Textarea className="md:col-span-2" aria-label={`Permission description ${index + 1}`} value={permission.description ?? ''} onChange={(event) => updatePermission(index, { description: event.target.value })} placeholder="Allows reading patient data" />
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
          {validationError ? <p className="text-sm text-destructive">{validationError}</p> : null}
        </section>

        <div className="flex justify-end">
          <Button type="submit" disabled={isLoading}>{isLoading ? 'Adding...' : 'Add Application'}</Button>
        </div>
      </form>
    </div>
  );
}
