import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { ApplicationPermissionStatusBadge } from './ApplicationPermissionStatusBadge';
import {
  useAddApplicationPermission,
  useChangeApplicationLifecycle,
  useManifestMutations,
  useMaintainerMutations,
  useRegisteredApplication,
  useTransferApplicationOwnership,
} from '../hooks';
import type { DestructiveOperationResult, ManifestPreview, PermissionManifestRequest } from '@/types';

export function RegisteredApplicationDetail({ id }: { id: string }) {
  const { data: application, isLoading } = useRegisteredApplication(id);
  const addPermission = useAddApplicationPermission(id);
  const [permissionDraft, setPermissionDraft] = useState({ name: '', description: '', category: '' });
  const manifestMutations = useManifestMutations(id, application?.concurrencyToken);
  const lifecycleMutation = useChangeApplicationLifecycle(id);
  const transferOwnershipMutation = useTransferApplicationOwnership(id);
  const maintainerMutations = useMaintainerMutations(id);
  const [manifestJson, setManifestJson] = useState('');
  const [manifestMessage, setManifestMessage] = useState<string | null>(null);
  const [manifestPreview, setManifestPreview] = useState<ManifestPreview | null>(null);
  const [manifestResult, setManifestResult] = useState<DestructiveOperationResult | null>(null);
  const [destructiveConfirmed, setDestructiveConfirmed] = useState(false);
  const [ownerDraft, setOwnerDraft] = useState({ ownerId: '', ownerType: 'User' });
  const [maintainerDraft, setMaintainerDraft] = useState({ principalId: '', principalType: 'User' });

  if (isLoading) {
    return <div>Loading application...</div>;
  }

  if (!application) {
    return <div>Application not found.</div>;
  }

  const parseManifestRequest = (): PermissionManifestRequest => {
    const manifest = JSON.parse(manifestJson);
    return {
      manifest,
      ownerId: application.ownerId,
      ownerType: application.ownerType as 'user' | 'group',
      manifestBaseUrl: application.manifestBaseUrl,
    };
  };

  const previewManifest = async () => {
    try {
      const preview = await manifestMutations.preview.mutateAsync(parseManifestRequest());
      setManifestPreview(preview);
      setManifestResult(null);
      setDestructiveConfirmed(false);
      setManifestMessage(preview.isDestructive ? 'Destructive changes detected' : 'Preview ready');
    } catch (error) {
      setManifestPreview(null);
      setManifestResult(null);
      setManifestMessage(error instanceof Error ? error.message : 'Manifest preview failed');
    }
  };

  const applyManifest = async () => {
    try {
      const result = await manifestMutations.apply.mutateAsync(parseManifestRequest());
      setManifestPreview(null);
      setManifestResult(result.result);
      setDestructiveConfirmed(false);
      setManifestMessage(result.result.removedPermissions.length > 0 ? 'Manifest applied with removals' : 'Manifest applied');
    } catch (error) {
      setManifestMessage(error instanceof Error ? error.message : 'Manifest apply failed');
    }
  };

  const previewRemoteManifest = async () => {
    try {
      const preview = await manifestMutations.previewRemote.mutateAsync();
      setManifestPreview(preview);
      setManifestResult(null);
      setDestructiveConfirmed(false);
      setManifestMessage(preview.isDestructive ? 'Remote preview found destructive changes' : 'Remote preview ready');
    } catch (error) {
      setManifestPreview(null);
      setManifestResult(null);
      setManifestMessage(error instanceof Error ? error.message : 'Remote preview failed');
    }
  };

  const applyRemoteManifest = async () => {
    try {
      const result = await manifestMutations.applyRemote.mutateAsync();
      setManifestPreview(null);
      setManifestResult(result.result);
      setDestructiveConfirmed(false);
      setManifestMessage(result.result.removedPermissions.length > 0 ? 'Remote manifest applied with removals' : 'Remote manifest applied');
    } catch (error) {
      setManifestMessage(error instanceof Error ? error.message : 'Remote apply failed');
    }
  };

  const applyDisabled =
    !manifestJson
    || manifestMutations.apply.isPending
    || (manifestPreview?.isDestructive === true && !destructiveConfirmed);
  const remoteApplyDisabled =
    !application.manifestBaseUrl
    || manifestMutations.applyRemote.isPending
    || (manifestPreview?.isDestructive === true && !destructiveConfirmed);

  const changeLifecycle = async (status: 'Active' | 'Disabled') => {
    await lifecycleMutation.mutateAsync({
      status,
      acknowledgeDependencies: true,
      concurrencyToken: application.concurrencyToken,
    });
  };

  const transferOwnership = async () => {
    await transferOwnershipMutation.mutateAsync({
      ownerId: ownerDraft.ownerId,
      ownerType: ownerDraft.ownerType,
      concurrencyToken: application.concurrencyToken,
    });
    setOwnerDraft({ ownerId: '', ownerType: 'User' });
  };

  const addMaintainer = async () => {
    await maintainerMutations.add.mutateAsync({
      principalId: maintainerDraft.principalId,
      principalType: maintainerDraft.principalType,
      concurrencyToken: application.concurrencyToken,
    });
    setMaintainerDraft({ principalId: '', principalType: 'User' });
  };

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-3xl font-bold">{application.displayName}</h1>
          <p className="text-muted-foreground">{application.applicationIdentifier}</p>
        </div>
        <ApplicationPermissionStatusBadge status={application.status} />
      </div>

      <section className="space-y-3 rounded-md border p-4">
        <h2 className="text-lg font-semibold">Lifecycle</h2>
        <div className="flex gap-2">
          <Button type="button" variant="outline" onClick={() => changeLifecycle('Active')} disabled={application.status === 'Active' || lifecycleMutation.isPending}>
            Enable
          </Button>
          <Button type="button" variant="outline" onClick={() => changeLifecycle('Disabled')} disabled={application.status === 'Disabled' || lifecycleMutation.isPending}>
            Disable
          </Button>
        </div>
      </section>

      {application.description && (
        <section className="rounded-md border p-4">
          <Label>Description</Label>
          <div className="mt-1 text-sm">{application.description}</div>
        </section>
      )}

      <section className="grid gap-4 rounded-md border p-4 md:grid-cols-3">
        <div>
          <Label>Manifest Version</Label>
          <div className="mt-1 text-sm">{application.manifestVersion}</div>
        </div>
        <div>
          <Label>Schema Version</Label>
          <div className="mt-1 text-sm">{application.schemaVersion}</div>
        </div>
        {application.manifestBaseUrl ? (
          <div>
            <Label>Manifest Base URL</Label>
            <div className="mt-1 text-sm">{application.manifestBaseUrl}</div>
          </div>
        ) : null}
      </section>

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
                  category: permissionDraft.category,
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
                  {permission.category && (
                    <div className="mt-1 text-xs text-muted-foreground">{permission.category}</div>
                  )}
                </div>
              </div>
            ))}
          </div>
      </section>

      <section className="space-y-4 rounded-md border p-4">
        <h2 className="text-lg font-semibold">Ownership</h2>
        <div className="grid gap-3 md:grid-cols-[1fr_12rem_auto]">
          <Input
            aria-label="New owner ID"
            placeholder={application.ownerId}
            value={ownerDraft.ownerId}
            onChange={(event) => setOwnerDraft({ ...ownerDraft, ownerId: event.target.value })}
          />
          <select
            aria-label="New owner type"
            className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            value={ownerDraft.ownerType}
            onChange={(event) => setOwnerDraft({ ...ownerDraft, ownerType: event.target.value })}
          >
            <option value="User">User</option>
            <option value="Group">Group</option>
          </select>
          <Button type="button" onClick={transferOwnership} disabled={!ownerDraft.ownerId || transferOwnershipMutation.isPending}>
            Transfer Ownership
          </Button>
        </div>
      </section>

      <section className="space-y-4 rounded-md border p-4">
        <h2 className="text-lg font-semibold">Maintainers</h2>
        <div className="grid gap-3 md:grid-cols-[1fr_12rem_auto]">
          <Input
            aria-label="Maintainer principal ID"
            value={maintainerDraft.principalId}
            onChange={(event) => setMaintainerDraft({ ...maintainerDraft, principalId: event.target.value })}
          />
          <select
            aria-label="Maintainer principal type"
            className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            value={maintainerDraft.principalType}
            onChange={(event) => setMaintainerDraft({ ...maintainerDraft, principalType: event.target.value })}
          >
            <option value="User">User</option>
            <option value="Group">Group</option>
          </select>
          <Button type="button" onClick={addMaintainer} disabled={!maintainerDraft.principalId || maintainerMutations.add.isPending}>
            Add Maintainer
          </Button>
        </div>
        <div className="space-y-2">
          {application.maintainers.map((maintainer) => (
            <div key={maintainer.id} className="flex items-center justify-between rounded-md border p-3">
              <div>
                <div className="font-medium">{maintainer.principalId}</div>
                <div className="text-sm text-muted-foreground">{maintainer.principalType}</div>
              </div>
              <Button
                type="button"
                variant="outline"
                onClick={() => maintainerMutations.remove.mutate({ principalId: maintainer.principalId, concurrencyToken: application.concurrencyToken })}
              >
                Remove
              </Button>
            </div>
          ))}
        </div>
      </section>

      <section className="space-y-4 rounded-md border p-4">
        <h2 className="text-lg font-semibold">Manifest Update</h2>
        <Textarea
          aria-label="Manifest JSON"
          className="min-h-72 font-mono"
          value={manifestJson}
          onChange={(event) => {
            setManifestJson(event.target.value);
            setManifestPreview(null);
            setManifestResult(null);
            setDestructiveConfirmed(false);
            setManifestMessage(null);
          }}
        />
        {manifestPreview ? (
          <div className="space-y-3 rounded-md border p-3">
            <div className="text-sm font-medium">
              {manifestPreview.currentManifestVersion} to {manifestPreview.requestedManifestVersion}
            </div>
            <div className="grid gap-3 md:grid-cols-3">
              <div className="text-sm">Additions: {manifestPreview.additions.length}</div>
              <div className="text-sm">Updates: {manifestPreview.metadataUpdates.length}</div>
              <div className="text-sm">Removals: {manifestPreview.removals.length}</div>
            </div>
            {manifestPreview.removals.length > 0 ? (
              <div className="space-y-2">
                <div className="text-sm font-medium text-destructive">Permissions to remove</div>
                <ul className="space-y-1 text-sm">
                  {manifestPreview.removals.map((permission) => (
                    <li key={permission.id}>{permission.fullPermissionKey}</li>
                  ))}
                </ul>
                <label className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={destructiveConfirmed}
                    onChange={(event) => setDestructiveConfirmed(event.target.checked)}
                  />
                  Confirm destructive manifest changes
                </label>
              </div>
            ) : null}
          </div>
        ) : null}
        {manifestResult ? (
          <div className="space-y-2 rounded-md border p-3">
            <div className="text-sm font-medium">Manifest result</div>
            <div className="text-sm">Removed permissions: {manifestResult.removedPermissions.length}</div>
            <div className="text-sm">Exact assignments removed: {manifestResult.exactAssignmentsRemoved.length}</div>
            <div className="text-sm">Wildcard assignments removed: {manifestResult.wildcardAssignmentsRemoved.length}</div>
          </div>
        ) : null}
        <div className="flex gap-2">
          <Button type="button" variant="outline" onClick={previewManifest} disabled={!manifestJson || manifestMutations.preview.isPending}>
            Preview Manifest
          </Button>
          <Button type="button" onClick={applyManifest} disabled={applyDisabled}>
            Apply Manifest
          </Button>
          {application.manifestBaseUrl ? (
            <>
              <Button type="button" variant="outline" onClick={previewRemoteManifest} disabled={manifestMutations.previewRemote.isPending}>
                Preview Remote Manifest
              </Button>
              <Button type="button" variant="outline" onClick={applyRemoteManifest} disabled={remoteApplyDisabled}>
                Apply Remote Manifest
              </Button>
            </>
          ) : null}
        </div>
        {manifestMessage ? <p className="text-sm">{manifestMessage}</p> : null}
      </section>
    </div>
  );
}
