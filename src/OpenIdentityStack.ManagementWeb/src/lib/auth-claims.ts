type AuthClaimsSource = {
  profile?: unknown;
  accessToken?: string | null;
};

const permissionClaimTypes = ['permission', 'permissions'];
const scopeClaimTypes = ['scope', 'scp'];
const standardScopes = new Set(['openid', 'profile', 'email', 'api', 'offline_access', 'roles']);

export function extractGrantedPermissions(source: AuthClaimsSource): string[] {
  const permissions = new Map<string, string>();

  addClaims(permissions, source.profile);

  const accessTokenPayload = decodeJwtPayload(source.accessToken);
  if (accessTokenPayload) {
    addClaims(permissions, accessTokenPayload);
  }

  return [...permissions.values()];
}

function addClaims(target: Map<string, string>, value: unknown): void {
  if (!isRecord(value)) {
    return;
  }

  for (const claimType of permissionClaimTypes) {
    addClaimValues(target, value[claimType]);
  }

  for (const claimType of scopeClaimTypes) {
    addScopeValues(target, value[claimType]);
  }
}

function addClaimValues(target: Map<string, string>, value: unknown): void {
  if (typeof value === 'string') {
    value
      .split(',')
      .map((entry) => entry.trim())
      .filter(Boolean)
      .forEach((entry) => addPermission(target, entry));
    return;
  }

  if (Array.isArray(value)) {
    value.forEach((entry) => addClaimValues(target, entry));
  }
}

function addScopeValues(target: Map<string, string>, value: unknown): void {
  if (typeof value === 'string') {
    value
      .split(' ')
      .map((entry) => entry.trim())
      .filter((entry) => entry && !standardScopes.has(entry.toLowerCase()))
      .forEach((entry) => addPermission(target, entry));
    return;
  }

  if (Array.isArray(value)) {
    value.forEach((entry) => addScopeValues(target, entry));
  }
}

function addPermission(target: Map<string, string>, permission: string): void {
  const normalized = permission.trim();
  if (!normalized) {
    return;
  }

  const key = normalized.toLowerCase();
  if (!target.has(key)) {
    target.set(key, normalized);
  }
}

function decodeJwtPayload(accessToken: string | null | undefined): unknown {
  if (!accessToken?.includes('.')) {
    return null;
  }

  const [, payload] = accessToken.split('.');
  if (!payload) {
    return null;
  }

  try {
    const normalized = payload.replaceAll('-', '+').replaceAll('_', '/');
    const padded = normalized.padEnd(normalized.length + (4 - (normalized.length % 4)) % 4, '=');
    return JSON.parse(atob(padded));
  } catch {
    return null;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}
