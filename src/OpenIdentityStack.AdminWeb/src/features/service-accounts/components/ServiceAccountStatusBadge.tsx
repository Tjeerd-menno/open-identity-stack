/**
 * ServiceAccountStatusBadge Component
 * 
 * Displays a colored badge for service account status
 */

import { Badge } from '@/components/ui/badge';
import type { ServiceAccountStatus } from '@/types';

interface ServiceAccountStatusBadgeProps {
  status: ServiceAccountStatus;
}

export function ServiceAccountStatusBadge({
  status,
}: ServiceAccountStatusBadgeProps) {
  const getVariant = (status: ServiceAccountStatus) => {
    switch (status) {
      case 'Active':
        return 'success';
      case 'Disabled':
        return 'destructive';
      default:
        return 'default';
    }
  };

  return (
    <Badge variant={getVariant(status)} data-status={status}>
      {status}
    </Badge>
  );
}
