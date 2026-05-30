import { Badge } from '@/components/ui/badge';
import type { ApplicationStatus } from '@/types';

interface ApplicationStatusBadgeProps {
  status: ApplicationStatus;
}

export function ApplicationStatusBadge({ status }: ApplicationStatusBadgeProps) {
  return (
    <Badge
      variant={status === 'Active' ? 'success' : 'secondary'}
      data-status={status}
    >
      {status}
    </Badge>
  );
}
