import { Badge } from '@/components/ui/badge';

/**
 * Provider type badge - currently only OIDC is supported
 */
export function ProviderTypeBadge() {
  // The system currently only supports OIDC providers
  return <Badge variant="default">OIDC</Badge>;
}
