import { Alert, Checkbox, Group, Loader, Stack, Text, Title } from '@mantine/core';
import { getApiErrorMessage } from '@/lib/admin-api';
import { usePlatformPermissionCatalog } from './roles-hooks';
import type { PlatformPermissionCatalogItem } from './roles-api';

type PermissionSelectorProps = {
  selectedPermissions: string[];
  onChange: (permissions: string[]) => void;
};

export function PermissionSelector({ selectedPermissions, onChange }: PermissionSelectorProps) {
  const catalog = usePlatformPermissionCatalog();

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

  const items = (catalog.data?.items ?? []).filter((item) => item.assignable);
  const groups = groupPermissions(items);

  function togglePermission(permission: string, checked: boolean) {
    if (checked) {
      onChange([...selectedPermissions, permission].filter(unique));
      return;
    }

    onChange(selectedPermissions.filter((selected) => selected !== permission));
  }

  return (
    <Stack gap="md" role="group" aria-label="Permission selector">
      {groups.map(([resource, permissions]) => (
        <Stack key={resource} gap="xs">
          <Title order={3} size="h5">{formatResource(resource)}</Title>
          <Stack gap={6}>
            {permissions.map((item) => (
              <Checkbox
                key={item.permission}
                label={item.displayName}
                description={item.kind === 'wildcard' ? `Broad grant: ${item.permission}` : item.permission}
                checked={selectedPermissions.includes(item.permission)}
                onChange={(event) => togglePermission(item.permission, event.currentTarget.checked)}
              />
            ))}
          </Stack>
        </Stack>
      ))}
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
