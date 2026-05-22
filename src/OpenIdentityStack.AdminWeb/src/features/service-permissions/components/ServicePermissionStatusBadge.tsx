import { Badge } from '@/components/ui/badge';

export function ServicePermissionStatusBadge({ status }: { status: string }) {
  const variant = status === 'Active' ? 'default' : status === 'Deprecated' ? 'secondary' : 'outline';
  return <Badge variant={variant}>{status}</Badge>;
}
