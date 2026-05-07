import { Badge } from '@/components/ui/badge';
import type { Role, RoleListItem } from '@/types';

interface RoleTypeBadgeProps {
  role: Role | RoleListItem;
  className?: string;
}

/**
 * Badge component to display role type (System vs Custom)
 * 
 * System roles are displayed with a distinctive badge to indicate they cannot be deleted
 * 
 * @param role - Role object
 * @param className - Additional CSS classes
 */
export function RoleTypeBadge({ role, className }: RoleTypeBadgeProps) {
  if (role.isSystemRole) {
    return (
      <Badge
        variant="secondary"
        className={className}
        data-system-role="true"
      >
        System
      </Badge>
    );
  }

  return (
    <Badge
      variant="outline"
      className={className}
    >
      Custom
    </Badge>
  );
}
