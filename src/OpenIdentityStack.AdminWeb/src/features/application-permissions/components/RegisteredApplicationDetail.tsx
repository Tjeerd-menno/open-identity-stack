import { useEffect, useMemo, useId, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useGroup } from '@/features/groups/hooks/useGroup';
import { useGroups } from '@/features/groups/hooks/useGroups';
import { useUser } from '@/features/users/hooks/useUser';
import { useUsers } from '@/features/users/hooks/useUsers';
import { ApplicationPermissionStatusBadge } from './ApplicationPermissionStatusBadge';
import {
  useAddApplicationPermission,
  useChangeApplicationLifecycle,
  useMaintainerMutations,
  useRegisteredApplication,
  useTransferApplicationOwnership,
  useUpdateRegisteredApplication,
} from '../hooks';
import type { GroupListItem, UserListItem } from '@/types';

type PrincipalType = 'User' | 'Group';

interface PrincipalOption {
  id: string;
  type: PrincipalType;
  label: string;
  detail: string;
}

interface PrincipalAutocompleteProps {
  label: string;
  placeholder: string;
  query: string;
  selected: { id: string; type: PrincipalType } | null;
  options: PrincipalOption[];
  isLoading: boolean;
  onQueryChange: (query: string) => void;
  onSelect: (option: PrincipalOption) => void;
}

function PrincipalAutocomplete({
  label,
  placeholder,
  query,
  selected,
  options,
  isLoading,
  onQueryChange,
  onSelect,
}: PrincipalAutocompleteProps) {
  const listboxId = useId();
  const containerRef = useRef<HTMLDivElement>(null);
  const [isOpen, setIsOpen] = useState(false);
  const normalizedQuery = query.trim().toLowerCase();
  const visibleOptions = useMemo(() => {
    if (!normalizedQuery) {
      return options.slice(0, 10);
    }

    return options
      .filter((option) => `${option.label} ${option.detail} ${option.type}`.toLowerCase().includes(normalizedQuery))
      .slice(0, 10);
  }, [normalizedQuery, options]);

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    const handlePointerDown = (event: PointerEvent) => {
      const target = event.target;
      if (target instanceof Node && containerRef.current?.contains(target)) {
        return;
      }

      setIsOpen(false);
    };

    document.addEventListener('pointerdown', handlePointerDown);
    return () => document.removeEventListener('pointerdown', handlePointerDown);
  }, [isOpen]);

  return (
    <div ref={containerRef} className="relative">
      <Input
        aria-autocomplete="list"
        aria-controls={listboxId}
        aria-expanded={isOpen}
        aria-label={label}
        aria-haspopup="listbox"
        role="combobox"
        placeholder={placeholder}
        value={query}
        onChange={(event) => {
          onQueryChange(event.target.value);
          setIsOpen(true);
        }}
        onFocus={() => setIsOpen(true)}
        onKeyDown={(event) => {
          if (event.key === 'Escape') {
            setIsOpen(false);
          }
        }}
      />
      {isOpen ? (
        <div
          id={listboxId}
          role="listbox"
          className="absolute z-50 mt-1 max-h-72 w-full overflow-auto rounded-md border bg-popover p-1 text-popover-foreground shadow-md"
        >
          {isLoading ? (
            <div className="px-3 py-2 text-sm text-muted-foreground">Loading principals...</div>
          ) : visibleOptions.length === 0 ? (
            <div className="px-3 py-2 text-sm text-muted-foreground">No users or groups found</div>
          ) : (
            visibleOptions.map((option) => (
              <button
                key={`${option.type}-${option.id}`}
                type="button"
                role="option"
                aria-label={`${option.label} ${option.detail} ${option.type}`}
                aria-selected={selected?.id === option.id && selected.type === option.type}
                className="flex w-full items-center justify-between gap-3 rounded-sm px-3 py-2 text-left text-sm hover:bg-accent hover:text-accent-foreground focus:bg-accent focus:text-accent-foreground focus:outline-none"
                onMouseDown={(event) => event.preventDefault()}
                onClick={() => {
                  onSelect(option);
                  setIsOpen(false);
                }}
              >
                <span className="min-w-0">
                  <span className="block truncate font-medium">{option.label}</span>
                  <span className="block truncate text-xs text-muted-foreground">{option.detail}</span>
                </span>
                <span className="shrink-0 rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
                  {option.type}
                </span>
              </button>
            ))
          )}
        </div>
      ) : null}
    </div>
  );
}

function mapUserOption(user: UserListItem): PrincipalOption {
  return {
    id: user.id,
    type: 'User',
    label: user.displayName || user.email,
    detail: user.email,
  };
}

function mapGroupOption(group: GroupListItem): PrincipalOption {
  return {
    id: group.id,
    type: 'Group',
    label: group.name,
    detail: group.description || group.id,
  };
}

function normalizePrincipalType(type: string | undefined | null): PrincipalType {
  return type?.toLowerCase() === 'group' ? 'Group' : 'User';
}

function isVersionLikeText(value: string | null | undefined): boolean {
  return /^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$/.test(value?.trim() ?? '');
}

function PrincipalDisplay({ principalId, principalType }: { principalId: string; principalType: string }) {
  const normalizedType = normalizePrincipalType(principalType);
  const { data: user, isLoading: userLoading } = useUser(normalizedType === 'User' ? principalId : '');
  const { data: group, isLoading: groupLoading } = useGroup(normalizedType === 'Group' ? principalId : '');
  const isLoading = normalizedType === 'Group' ? groupLoading : userLoading;
  const label = normalizedType === 'Group'
    ? group?.name
    : user?.displayName || user?.email;
  const detail = normalizedType === 'Group'
    ? group?.description
    : user?.email;

  return (
    <div>
      <div className="font-medium">
        {isLoading ? 'Loading...' : label ?? `Unknown ${normalizedType.toLowerCase()}`}
      </div>
      <div className="text-sm text-muted-foreground">
        {detail ? `${detail} ` : null}
        <span className="rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
          {normalizedType}
        </span>
      </div>
    </div>
  );
}

export function RegisteredApplicationDetail({ id }: { id: string }) {
  const { data: application, isLoading } = useRegisteredApplication(id);
  const addPermission = useAddApplicationPermission(id);
  const [permissionDraft, setPermissionDraft] = useState({ name: '', description: '', category: '' });
  const lifecycleMutation = useChangeApplicationLifecycle(id);
  const transferOwnershipMutation = useTransferApplicationOwnership(id);
  const updateApplication = useUpdateRegisteredApplication(id);
  const maintainerMutations = useMaintainerMutations(id);
  const [ownerDraft, setOwnerDraft] = useState<{ ownerId: string; ownerType: PrincipalType }>({ ownerId: '', ownerType: 'User' });
  const [ownerSearch, setOwnerSearch] = useState('');
  const [maintainerDraft, setMaintainerDraft] = useState<{ principalId: string; principalType: PrincipalType }>({ principalId: '', principalType: 'User' });
  const [maintainerSearch, setMaintainerSearch] = useState('');
  const [isEditingDetails, setIsEditingDetails] = useState(false);
  const [manifestBaseUrlDraft, setManifestBaseUrlDraft] = useState('');
  const { data: usersData, isLoading: usersLoading } = useUsers({ page: 1, pageSize: 20, search: ownerSearch, enabled: isEditingDetails });
  const { data: groupsData, isLoading: groupsLoading } = useGroups({ page: 1, pageSize: 20, search: ownerSearch, enabled: isEditingDetails });
  const { data: maintainerUsersData, isLoading: maintainerUsersLoading } = useUsers({ page: 1, pageSize: 20, search: maintainerSearch, enabled: isEditingDetails });
  const { data: maintainerGroupsData, isLoading: maintainerGroupsLoading } = useGroups({ page: 1, pageSize: 20, search: maintainerSearch, enabled: isEditingDetails });
  const currentOwnerType = normalizePrincipalType(application?.ownerType);
  const currentOwnerUserId = application && currentOwnerType === 'User' ? application.ownerId : '';
  const currentOwnerGroupId = application && currentOwnerType === 'Group' ? application.ownerId : '';
  const { data: currentOwnerUser, isLoading: currentOwnerUserLoading } = useUser(currentOwnerUserId);
  const { data: currentOwnerGroup, isLoading: currentOwnerGroupLoading } = useGroup(currentOwnerGroupId);
  const currentOwnerName = currentOwnerType === 'Group'
    ? currentOwnerGroup?.name
    : currentOwnerUser?.displayName || currentOwnerUser?.email;
  const currentOwnerLoading = currentOwnerType === 'Group' ? currentOwnerGroupLoading : currentOwnerUserLoading;
  const isManifestBacked = !!application?.manifestBaseUrl;
  const ownerOptions = useMemo(
    () => [
      ...(usersData?.items.map(mapUserOption) ?? []),
      ...(groupsData?.items.map(mapGroupOption) ?? []),
    ],
    [groupsData?.items, usersData?.items]
  );
  const maintainerOptions = useMemo(
    () => [
      ...(maintainerUsersData?.items.map(mapUserOption) ?? []),
      ...(maintainerGroupsData?.items.map(mapGroupOption) ?? []),
    ],
    [maintainerGroupsData?.items, maintainerUsersData?.items]
  );
  const applicationDescription = isVersionLikeText(application?.description) ? null : application?.description?.trim();

  if (isLoading) {
    return <div>Loading application...</div>;
  }

  if (!application) {
    return <div>Application not found.</div>;
  }

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
    setOwnerSearch('');
  };

  const addMaintainer = async () => {
    await maintainerMutations.add.mutateAsync({
      principalId: maintainerDraft.principalId,
      principalType: maintainerDraft.principalType,
      concurrencyToken: application.concurrencyToken,
    });
    setMaintainerDraft({ principalId: '', principalType: 'User' });
    setMaintainerSearch('');
  };

  const updateManifestBaseUrl = async () => {
    await updateApplication.mutateAsync({
      displayName: application.displayName,
      description: application.description,
      manifestBaseUrl: manifestBaseUrlDraft,
      concurrencyToken: application.concurrencyToken,
    });
    setIsEditingDetails(false);
  };

  return (
    <div className="space-y-6">
      <section aria-label="Application details" className="overflow-hidden rounded-md border bg-background">
        <div className="flex flex-col gap-3 border-b bg-muted/20 px-5 py-4 md:flex-row md:items-start md:justify-between">
          <div className="min-w-0">
            <h1 className="break-words text-3xl font-bold">{application.displayName}</h1>
            <p className="break-words text-sm text-muted-foreground">{application.applicationIdentifier}</p>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <ApplicationPermissionStatusBadge status={application.status} />
            <Button
              type="button"
              variant="outline"
              onClick={() => {
                if (!isEditingDetails) {
                  setManifestBaseUrlDraft(application.manifestBaseUrl ?? '');
                }

                setIsEditingDetails((value) => !value);
              }}
            >
              {isEditingDetails ? 'Done' : 'Edit'}
            </Button>
          </div>
        </div>

        <div className="grid gap-6 p-5 lg:grid-cols-[minmax(0,1fr)_minmax(18rem,0.8fr)]">
          <div className="space-y-5">
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
              {applicationDescription && (
                <div className="space-y-1">
                  <Label>Description</Label>
                  <div className="break-words text-sm">{applicationDescription}</div>
                </div>
              )}
              <div className="space-y-1">
                <Label>Manifest Version</Label>
                <div className="break-words text-sm text-muted-foreground">{application.manifestVersion || 'Not set'}</div>
              </div>
              <div className="space-y-1">
                <Label>Schema Version</Label>
                <div className="break-words text-sm text-muted-foreground">{application.schemaVersion || 'Not set'}</div>
              </div>
              {application.manifestBaseUrl ? (
                <div className="space-y-1 sm:col-span-2 xl:col-span-3">
                  <Label>Manifest Base URL</Label>
                  <div className="break-words text-sm text-muted-foreground">{application.manifestBaseUrl}</div>
                </div>
              ) : null}
            </div>

            {isEditingDetails && isManifestBacked ? (
              <div className="grid gap-3 border-t pt-5 md:grid-cols-[1fr_auto]">
                <div className="space-y-2">
                  <Label htmlFor="manifest-base-url">Manifest Base URL</Label>
                  <Input
                    id="manifest-base-url"
                    value={manifestBaseUrlDraft}
                    onChange={(event) => setManifestBaseUrlDraft(event.target.value)}
                  />
                </div>
                <Button
                  className="md:self-end"
                  type="button"
                  onClick={updateManifestBaseUrl}
                  disabled={!manifestBaseUrlDraft.trim() || updateApplication.isPending}
                >
                  Save URL
                </Button>
              </div>
            ) : null}

            {isEditingDetails && !isManifestBacked ? (
              <div className="flex justify-end border-t pt-5">
                {application.status === 'Active' ? (
                  <Button type="button" variant="outline" onClick={() => changeLifecycle('Disabled')} disabled={lifecycleMutation.isPending}>
                    Disable
                  </Button>
                ) : (
                  <Button type="button" variant="outline" onClick={() => changeLifecycle('Active')} disabled={lifecycleMutation.isPending}>
                    Enable
                  </Button>
                )}
              </div>
            ) : null}
          </div>

          <div className="space-y-5 lg:border-l lg:pl-6">
            <div className="space-y-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h2 className="text-base font-semibold">Ownership</h2>
                <div className="flex flex-wrap items-center gap-2 text-sm">
                  <span className="text-muted-foreground">Current owner</span>
                  <span className="font-medium">
                    {currentOwnerLoading ? 'Loading...' : currentOwnerName ?? `Unknown ${currentOwnerType.toLowerCase()}`}
                  </span>
                  <span className="rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
                    {currentOwnerType}
                  </span>
                </div>
              </div>
              {isEditingDetails && !isManifestBacked ? (
                <div className="grid gap-3 xl:grid-cols-[1fr_auto]">
                  <PrincipalAutocomplete
                    label="New owner"
                    placeholder="Search users or groups..."
                    query={ownerSearch}
                    selected={ownerDraft.ownerId ? { id: ownerDraft.ownerId, type: ownerDraft.ownerType } : null}
                    options={ownerOptions}
                    isLoading={usersLoading || groupsLoading}
                    onQueryChange={(query) => {
                      setOwnerSearch(query);
                      setOwnerDraft({ ownerId: '', ownerType: 'User' });
                    }}
                    onSelect={(option) => {
                      setOwnerDraft({ ownerId: option.id, ownerType: option.type });
                      setOwnerSearch(option.label);
                    }}
                  />
                  <Button type="button" onClick={transferOwnership} disabled={!ownerDraft.ownerId || transferOwnershipMutation.isPending}>
                    Transfer Ownership
                  </Button>
                </div>
              ) : null}
            </div>

            <div className="space-y-3 border-t pt-5">
              <h2 className="text-base font-semibold">Maintainers</h2>
              {isEditingDetails && !isManifestBacked ? (
                <div className="grid gap-3 xl:grid-cols-[1fr_auto]">
                  <PrincipalAutocomplete
                    label="New maintainer"
                    placeholder="Search users or groups..."
                    query={maintainerSearch}
                    selected={maintainerDraft.principalId ? { id: maintainerDraft.principalId, type: maintainerDraft.principalType } : null}
                    options={maintainerOptions}
                    isLoading={maintainerUsersLoading || maintainerGroupsLoading}
                    onQueryChange={(query) => {
                      setMaintainerSearch(query);
                      setMaintainerDraft({ principalId: '', principalType: 'User' });
                    }}
                    onSelect={(option) => {
                      setMaintainerDraft({ principalId: option.id, principalType: option.type });
                      setMaintainerSearch(option.label);
                    }}
                  />
                  <Button type="button" onClick={addMaintainer} disabled={!maintainerDraft.principalId || maintainerMutations.add.isPending}>
                    Add Maintainer
                  </Button>
                </div>
              ) : null}
              <div className="space-y-2">
                {application.maintainers.map((maintainer) => (
                  <div key={maintainer.id} className="flex items-center justify-between gap-3 rounded-md border px-3 py-2">
                    <PrincipalDisplay principalId={maintainer.principalId} principalType={maintainer.principalType} />
                    {isEditingDetails && !isManifestBacked ? (
                      <Button
                        type="button"
                        variant="outline"
                        onClick={() => maintainerMutations.remove.mutate({ principalId: maintainer.principalId, concurrencyToken: application.concurrencyToken })}
                      >
                        Remove
                      </Button>
                    ) : null}
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      </section>

      <section className="space-y-4 rounded-md border p-4">
        <h2 className="text-lg font-semibold">Permissions</h2>
        {!isManifestBacked ? (
          <div className="grid gap-3 md:grid-cols-[1fr_1fr_auto]">
            <Input
              aria-label="Permission name"
              placeholder="patient:read"
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
        ) : null}
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
    </div>
  );
}
