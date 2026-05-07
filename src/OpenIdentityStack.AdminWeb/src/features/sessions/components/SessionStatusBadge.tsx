/**
 * SessionStatusBadge Component
 * 
 * Displays a visual badge indicating session status
 */

import { Badge } from '@/components/ui/badge';
import type { SessionStatus } from '@/types';

interface SessionStatusBadgeProps {
  status: SessionStatus;
}

export function SessionStatusBadge({ status }: SessionStatusBadgeProps) {
  const variants: Record<SessionStatus, { variant: 'default' | 'secondary' | 'destructive' | 'outline'; label: string }> = {
    Active: { variant: 'default', label: 'Active' },
    Expired: { variant: 'secondary', label: 'Expired' },
    Revoked: { variant: 'destructive', label: 'Revoked' },
    LoggedOut: { variant: 'outline', label: 'Logged Out' },
  };

  const { variant, label } = variants[status];

  return <Badge variant={variant}>{label}</Badge>;
}
