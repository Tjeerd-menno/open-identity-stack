export const allPermissions = '*';

type PermissionSource = {
  profile?: unknown;
  accessToken?: string | null;
};

const standardScopes = new Set(['openid', 'profile', 'email', 'api', 'offline_access', 'roles']);

const splitPermissionClaim = (value: string) =>
  value
    .split(',')
    .map((permission) => permission.trim())
    .filter(Boolean);

const splitScopeClaim = (value: string) =>
  value
    .split(/\s+/)
    .map((scope) => scope.trim())
    .filter((scope) => scope.length > 0 && !standardScopes.has(scope.toLowerCase()));

const addDeduplicated = (permissions: string[], seen: Set<string>, value: string) => {
  const normalized = value.toLowerCase();

  if (!seen.has(normalized)) {
    seen.add(normalized);
    permissions.push(value);
  }
};

const collectClaimValues = (
  value: unknown,
  split: (value: string) => string[],
  permissions: string[],
  seen: Set<string>
) => {
  if (typeof value === 'string') {
    for (const permission of split(value)) {
      addDeduplicated(permissions, seen, permission);
    }

    return;
  }

  if (Array.isArray(value)) {
    for (const item of value) {
      collectClaimValues(item, split, permissions, seen);
    }
  }
};

const collectFromClaims = (claims: unknown, permissions: string[], seen: Set<string>) => {
  if (!claims || typeof claims !== 'object' || Array.isArray(claims)) {
    return;
  }

  const record = claims as Record<string, unknown>;

  collectClaimValues(record['permission'], splitPermissionClaim, permissions, seen);
  collectClaimValues(record['permissions'], splitPermissionClaim, permissions, seen);
  collectClaimValues(record['scope'], splitScopeClaim, permissions, seen);
  collectClaimValues(record['scp'], splitScopeClaim, permissions, seen);
};

const decodeJwtPayload = (token: string) => {
  const [, payload] = token.split('.');

  if (!payload) {
    return undefined;
  }

  try {
    const normalizedPayload = payload.replaceAll('-', '+').replaceAll('_', '/');
    const paddedPayload = normalizedPayload.padEnd(
      normalizedPayload.length + ((4 - (normalizedPayload.length % 4)) % 4),
      '='
    );

    return JSON.parse(atob(paddedPayload)) as unknown;
  } catch {
    return undefined;
  }
};

export const extractGrantedPermissions = (source: PermissionSource): string[] => {
  const permissions: string[] = [];
  const seen = new Set<string>();

  collectFromClaims(source.profile, permissions, seen);

  if (source.accessToken) {
    collectFromClaims(decodeJwtPayload(source.accessToken), permissions, seen);
  }

  return permissions;
};

export const hasPermission = (
  grantedPermissions: string[],
  requiredPermission: string
): boolean => {
  const required = requiredPermission.trim().toLowerCase();

  if (!required) {
    return false;
  }

  return grantedPermissions.some((grantedPermission) => {
    const granted = grantedPermission.trim().toLowerCase();

    if (granted === required) {
      return true;
    }

    if (granted === allPermissions) {
      const [resource, action, extra] = required.split(':');

      return Boolean(resource && action && !extra);
    }

    if (granted.endsWith(':*')) {
      const prefix = granted.slice(0, -2);
      const prefixSegments = prefix.split(':');
      const requiredSegments = required.split(':');

      return (
        requiredSegments.length === prefixSegments.length + 1 &&
        prefixSegments.every((segment, index) => requiredSegments[index] === segment) &&
        requiredSegments.at(-1) !== ''
      );
    }

    return false;
  });
};

export const hasAnyPermission = (
  grantedPermissions: string[],
  requiredPermissions: string[]
): boolean =>
  requiredPermissions.some((requiredPermission) =>
    hasPermission(grantedPermissions, requiredPermission)
  );

export const hasEveryPermission = (
  grantedPermissions: string[],
  requiredPermissions: string[]
): boolean =>
  requiredPermissions.every((requiredPermission) =>
    hasPermission(grantedPermissions, requiredPermission)
  );
