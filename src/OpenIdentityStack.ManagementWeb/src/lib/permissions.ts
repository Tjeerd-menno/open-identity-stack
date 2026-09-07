export {
  allPermissions,
  hasAnyPermission,
  hasEveryPermission,
  hasPermission,
} from '@openidentitystack/admin-api-client';

export const credentialCutoverPermissions = ['sessions:revoke', 'users:read', 'applications:read'];
