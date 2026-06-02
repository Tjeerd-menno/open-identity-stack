import { Badge } from '@mantine/core';
import type { ApplicationStatus } from './applications-api';

type ApplicationStatusBadgeProps = {
  status: ApplicationStatus;
};

export function ApplicationStatusBadge({ status }: ApplicationStatusBadgeProps) {
  return (
    <Badge color={status === 'Active' ? 'green' : 'gray'} variant="light" data-status={status}>
      {status}
    </Badge>
  );
}
