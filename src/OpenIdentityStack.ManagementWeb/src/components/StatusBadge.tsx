import { Badge, type MantineColor } from '@mantine/core';

const statusColors: Record<string, MantineColor> = {
  active: 'green',
  available: 'green',
  enabled: 'green',
  disabled: 'yellow',
  expired: 'yellow',
  loggedout: 'gray',
  revoked: 'red',
  error: 'red',
  failed: 'red',
  pending: 'blue',
};

export function StatusBadge({ status, color }: { status: string; color?: MantineColor }) {
  return (
    <Badge color={color ?? statusColors[status.toLowerCase()] ?? 'gray'} variant="light">
      {status}
    </Badge>
  );
}

