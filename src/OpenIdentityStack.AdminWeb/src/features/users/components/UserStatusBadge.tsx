/**
 * UserStatusBadge Component
 * 
 * Displays a colored badge for user status
 */

import { Badge } from '@/components/ui/badge';
import type { UserStatus } from '@/types';

interface UserStatusBadgeProps {
  status: UserStatus;
}

export function UserStatusBadge({ status }: UserStatusBadgeProps) {
  const getVariant = (status: UserStatus) => {
    switch (status) {
      case 'Active':
        return 'success';
      case 'Disabled':
        return 'destructive';
      case 'Locked':
        return 'destructive';
      case 'PendingVerification':
        return 'warning';
      default:
        return 'default';
    }
  };

  const getLabel = (status: UserStatus) => {
    switch (status) {
      case 'PendingVerification':
        return 'Pending';
      default:
        return status;
    }
  };

  return (
    <Badge variant={getVariant(status)} data-status={status}>
      {getLabel(status)}
    </Badge>
  );
}
