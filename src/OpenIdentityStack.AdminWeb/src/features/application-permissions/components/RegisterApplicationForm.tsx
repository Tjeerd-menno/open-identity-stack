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
  name: '',
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
  const [applicationName, setApplicationName] = useState('');
  const [applicationVersion, setApplicationVersion] = useState('');
  const [endpoint, setEndpoint] = useState('');
  const [permissions, setPermissions] = useState<PermissionManifestPermission[]>([{ ...emptyPermission }]);

  const updatePermission = (index: number, patch: Partial<PermissionManifestPermission>) => {
    setPermissions((current) => current.map((permission, currentIndex) =>
      currentIndex === index ? { ...permission, ...patch } : permission
    ));
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    await onSubmit({
      application: {
        id: applicationId,
        name: applicationName,
        version: applicationVersion || null,
      },
      permissions: permissions.map((permission) => ({
        name: permission.name,
        description: permission.description,
        category: permission.category || null,
      })),
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
              <Label htmlFor="applicationName">Application Name</Label>
              <Input id="applicationName" value={applicationName} onChange={(event) => setApplicationName(event.target.value)} placeholder="Patient API" required />
            </div>
            <div className="space-y-2">
              <Label htmlFor="applicationVersion">Version</Label>
              <Input id="applicationVersion" value={applicationVersion} onChange={(event) => setApplicationVersion(event.target.value)} placeholder="1.0.0" />
            </div>
          </div>
        </section>

        <section className="space-y-4 rounded-md border p-4">
          <h2 className="text-lg font-semibold">Permissions</h2>
          {permissions.map((permission, index) => (
            <div key={index} className="grid gap-3 rounded-md border p-4 md:grid-cols-2">
              <Input aria-label={`Permission name ${index + 1}`} value={permission.name} onChange={(event) => updatePermission(index, { name: event.target.value })} placeholder="read:patients" required />
              <Input aria-label={`Permission category ${index + 1}`} value={permission.category ?? ''} onChange={(event) => updatePermission(index, { category: event.target.value })} placeholder="Patients" />
              <Textarea className="md:col-span-2" aria-label={`Permission description ${index + 1}`} value={permission.description ?? ''} onChange={(event) => updatePermission(index, { description: event.target.value })} placeholder="Allows reading patient data" required />
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
        </section>

        <div className="flex justify-end">
          <Button type="submit" disabled={isLoading}>{isLoading ? 'Adding...' : 'Add Application'}</Button>
        </div>
      </form>
    </div>
  );
}
