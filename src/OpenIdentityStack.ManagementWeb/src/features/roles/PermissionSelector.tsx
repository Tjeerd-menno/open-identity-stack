import { Alert, Checkbox, Group, Loader, Select, Stack, Switch, Text, TextInput, Title } from '@mantine/core';
import { useMemo, useState } from 'react';
import { getApiErrorMessage } from '@/lib/admin-api';
import { usePlatformPermissionCatalog } from './roles-hooks';
import type { PlatformPermissionCatalogItem } from './roles-api';

type PermissionSelectorProps = {
  selectedPermissions: string[];
  onChange: (permissions: string[]) => void;
};

export function PermissionSelector({ selectedPermissions, onChange }: PermissionSelectorProps) {
  const catalog = usePlatformPermissionCatalog();
  const [search, setSearch] = useState('');
  const [resource, setResource] = useState<string | null>(null);
  const [kind, setKind] = useState<string | null>(null);
  const [selectedOnly, setSelectedOnly] = useState(false);
  const items = (catalog.data?.items ?? []).filter((item) => item.assignable);
  const resourceOptions = useMemo(
    () => Array.from(new Set(items.map((item) => item.resource))).sort().map((value) => ({ value, label: formatResource(value) })),
    [items]
  );

  if (catalog.isLoading) {
    return (
      <Group gap="sm">
        <Loader size="sm" aria-label="Loading permissions" />
        <Text size="sm" c="dimmed">Loading permissions</Text>
      </Group>
    );
  }

  if (catalog.isError) {
    return <Alert color="red">{getApiErrorMessage(catalog.error)}</Alert>;
  }

  const filteredItems = items.filter((item) => {
    const matchesSearch = search.trim().length === 0
      || item.permission.toLowerCase().includes(search.toLowerCase())
      || item.displayName.toLowerCase().includes(search.toLowerCase());
    const matchesResource = !resource || item.resource === resource;
    const matchesKind = !kind || item.kind === kind;
    const matchesSelected = !selectedOnly || isPermissionChecked(item.permission, selectedPermissions);
    return matchesSearch && matchesResource && matchesKind && matchesSelected;
  });
  const groups = groupPermissions(filteredItems);

  function togglePermission(permission: string, checked: boolean) {
    if (checked) {
      onChange([...selectedPermissions, permission].filter(unique));
      return;
    }

    onChange(selectedPermissions.filter((selected) => selected !== permission));
  }

  return (
    <Stack gap="md" role="group" aria-label="Permission selector">
      <Group align="flex-end">
        <TextInput
          label="Search permissions"
          placeholder="Search by permission or display name..."
          value={search}
          onChange={(event) => setSearch(event.currentTarget.value)}
        />
        <Select
          clearable
          data={resourceOptions}
          label="Resource"
          searchable
          value={resource}
          onChange={setResource}
        />
        <Select
          clearable
          data={[
            { value: 'wildcard', label: 'Wildcard' },
            { value: 'action', label: 'Action' },
          ]}
          label="Kind"
          value={kind}
          onChange={setKind}
        />
        <Switch
          checked={selectedOnly}
          label="Selected only"
          onChange={(event) => setSelectedOnly(event.currentTarget.checked)}
        />
      </Group>
      {groups.map(([resource, permissions]) => (
        <Stack key={resource} gap="xs">
          <Title order={3} size="h5">{formatResource(resource)}</Title>
          <Stack gap={6}>
            {permissions.map((item) => (
              <Checkbox
                key={item.permission}
                label={item.displayName}
                description={item.kind === 'wildcard' ? `Broad grant: ${item.permission}` : item.permission}
                checked={isPermissionChecked(item.permission, selectedPermissions)}
                onChange={(event) => togglePermission(item.permission, event.currentTarget.checked)}
              />
            ))}
          </Stack>
        </Stack>
      ))}
      {groups.length === 0 && <Text c="dimmed">No permissions match the current filters.</Text>}
    </Stack>
  );
}

function groupPermissions(items: PlatformPermissionCatalogItem[]) {
  const grouped = new Map<string, PlatformPermissionCatalogItem[]>();

  items.forEach((item) => {
    const current = grouped.get(item.resource) ?? [];
    current.push(item);
    grouped.set(item.resource, current);
  });

  return Array.from(grouped.entries())
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([resource, permissions]) => [
      resource,
      permissions.sort((left, right) => {
        if (left.kind !== right.kind) {
          return left.kind === 'wildcard' ? -1 : 1;
        }

        return left.permission.localeCompare(right.permission);
      }),
    ] as const);
}

function formatResource(resource: string) {
  if (resource === '*') {
    return 'All permissions';
  }

  return resource
    .split('-')
    .map((part) => `${part.slice(0, 1).toUpperCase()}${part.slice(1)}`)
    .join(' ');
}

function unique(value: string, index: number, array: string[]) {
  return array.indexOf(value) === index;
}

function isPermissionChecked(permission: string, selectedPermissions: string[]) {
  if (selectedPermissions.includes(permission)) {
    return true;
  }

  if (permission === '*') {
    return false;
  }

  return selectedPermissions.some((selectedPermission) => {
    if (selectedPermission === '*') {
      return true;
    }

    if (!selectedPermission.endsWith(':*') || selectedPermission === permission) {
      return false;
    }

    return permission.startsWith(selectedPermission.slice(0, -1));
  });
}
