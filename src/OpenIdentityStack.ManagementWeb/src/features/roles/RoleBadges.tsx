import { Badge } from '@mantine/core';

type RoleTypeBadgeProps = {
  isSystemRole: boolean;
};

type RoleStatusBadgeProps = {
  isActive: boolean;
};

export function RoleTypeBadge({ isSystemRole }: RoleTypeBadgeProps) {
  return (
    <Badge color={isSystemRole ? 'blue' : 'gray'} variant="light">
      {isSystemRole ? 'System' : 'Custom'}
    </Badge>
  );
}

export function RoleStatusBadge({ isActive }: RoleStatusBadgeProps) {
  return (
    <Badge color={isActive ? 'green' : 'red'} variant="light">
      {isActive ? 'Active' : 'Inactive'}
    </Badge>
  );
}
