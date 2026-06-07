# Concentrate Permission Semantics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move frontend permission extraction and matching into one shared Permission Semantics module consumed by both Management Web and AdminWeb.

**Architecture:** Create a focused shared TypeScript module at `src/frontend-packages/permission-semantics/src` that owns OIDC permission extraction and permission matching. Keep UI-local files as adapters so existing imports in Management Web and AdminWeb remain stable. Change AdminWeb behavior to match the backend-anchored Permission Semantics already used by Management Web: role names alone do not imply wildcard access.

**Tech Stack:** TypeScript, React, Vite, Vitest, Testing Library.

---

## File Structure

- Create: `src/frontend-packages/permission-semantics/src/index.ts`
  - Shared module interface.
  - Exports `extractGrantedPermissions`, `hasPermission`, `hasAnyPermission`, `hasEveryPermission`, and `allPermissions`.
- Create: `src/OpenIdentityStack.ManagementWeb/src/lib/permission-semantics-shared.test.ts`
  - Tests the shared module through the Management Web Vitest runner.
- Modify: `src/OpenIdentityStack.ManagementWeb/src/lib/auth-claims.ts`
  - Compatibility adapter that re-exports `extractGrantedPermissions`.
- Modify: `src/OpenIdentityStack.ManagementWeb/src/lib/permissions.ts`
  - Compatibility adapter that re-exports permission matching helpers.
- Modify: `src/OpenIdentityStack.ManagementWeb/tsconfig.app.json`
  - Adds alias path for `@openidentitystack/permission-semantics`.
- Modify: `src/OpenIdentityStack.ManagementWeb/vite.config.ts`
  - Adds Vite alias for `@openidentitystack/permission-semantics`.
- Modify: `src/OpenIdentityStack.AdminWeb/src/features/auth/services/oidc-config.ts`
  - Uses the shared module for `extractPermissions`.
  - Keeps `extractDisplayName` and OIDC settings local.
- Modify: `src/OpenIdentityStack.AdminWeb/src/features/auth/components/ProtectedRoute.tsx`
  - Uses shared `hasEveryPermission` instead of direct string inclusion.
- Modify: `src/OpenIdentityStack.AdminWeb/src/features/auth/hooks/useAuth.ts`
  - Uses shared `hasPermission`, `hasAnyPermission`, and `hasEveryPermission`.
- Modify: `src/OpenIdentityStack.AdminWeb/tsconfig.app.json`
  - Adds alias path for `@openidentitystack/permission-semantics`.
- Modify: `src/OpenIdentityStack.AdminWeb/vite.config.ts`
  - Adds Vite alias for `@openidentitystack/permission-semantics`.
- Test: `src/OpenIdentityStack.ManagementWeb/src/lib/auth-claims.test.ts`
  - Existing tests should remain green through adapter exports.
- Test: `src/OpenIdentityStack.ManagementWeb/src/lib/permissions.test.ts`
  - Existing tests should remain green through adapter exports.
- Test: `src/OpenIdentityStack.AdminWeb/src/features/auth/services/oidc-config.test.ts`
  - Update role-name behavior and add aggregation coverage.
- Test: `src/OpenIdentityStack.AdminWeb/src/features/auth/components/ProtectedRoute.test.tsx`
  - Add wildcard guard coverage.
- Test: `src/OpenIdentityStack.AdminWeb/src/features/auth/hooks/useAuth.test.tsx`
  - Add wildcard hook coverage.

---

## Task 1: Add Shared Permission Semantics Module

**Files:**
- Create: `src/frontend-packages/permission-semantics/src/index.ts`
- Create: `src/OpenIdentityStack.ManagementWeb/src/lib/permission-semantics-shared.test.ts`

- [ ] **Step 1: Write the failing shared-module tests**

Create `src/OpenIdentityStack.ManagementWeb/src/lib/permission-semantics-shared.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import {
  extractGrantedPermissions,
  hasAnyPermission,
  hasEveryPermission,
  hasPermission,
} from '@openidentitystack/permission-semantics';

const jwtWithPayload = (payload: object) => {
  const encodedPayload = btoa(JSON.stringify(payload))
    .replaceAll('+', '-')
    .replaceAll('/', '_')
    .replaceAll('=', '');
  return `header.${encodedPayload}.signature`;
};

describe('shared permission semantics', () => {
  it('extracts explicit permissions and non-standard scopes from profile and token claims', () => {
    const token = jwtWithPayload({
      permissions: ['roles:read'],
      scp: ['groups:read', 'openid'],
    });

    expect(
      extractGrantedPermissions({
        profile: {
          permission: 'users:read, users:write',
          scope: 'openid profile applications:read',
        },
        accessToken: token,
      })
    ).toEqual(['users:read', 'users:write', 'applications:read', 'roles:read', 'groups:read']);
  });

  it('does not infer wildcard permission from admin role names', () => {
    expect(
      extractGrantedPermissions({
        profile: {
          role: 'admin',
          roles: ['super-admin'],
          'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'admin',
        },
      })
    ).toEqual([]);
  });

  it('deduplicates permissions case-insensitively while preserving first observed casing', () => {
    expect(
      extractGrantedPermissions({
        profile: {
          permission: ['Users:Read', 'users:read', 'roles:read'],
        },
      })
    ).toEqual(['Users:Read', 'roles:read']);
  });

  it('matches global, resource, and nested resource wildcards', () => {
    expect(hasPermission(['*'], 'users:read')).toBe(true);
    expect(hasPermission(['*'], 'application-permissions:read')).toBe(false);
    expect(hasPermission(['applications:*'], 'applications:manage-credentials')).toBe(true);
    expect(hasPermission(['patient-api:records:*'], 'patient-api:records:read')).toBe(true);
    expect(hasPermission(['applications:*'], 'roles:read')).toBe(false);
  });

  it('evaluates any and every collections through the same matcher', () => {
    expect(hasAnyPermission(['users:*'], ['roles:read', 'users:write'])).toBe(true);
    expect(hasEveryPermission(['users:*'], ['users:read', 'users:update'])).toBe(true);
    expect(hasEveryPermission(['users:read'], ['users:read', 'users:update'])).toBe(false);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
cd src\OpenIdentityStack.ManagementWeb
npm test -- src/lib/permission-semantics-shared.test.ts
```

Expected: FAIL because `@openidentitystack/permission-semantics` cannot be resolved.

- [ ] **Step 3: Add the shared module implementation**

Create `src/frontend-packages/permission-semantics/src/index.ts`:

```ts
export const allPermissions = '*';

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

export function hasPermission(grantedPermissions: string[], requiredPermission: string): boolean {
  const requiredParts = requiredPermission.split(':');

  return grantedPermissions.some((grantedPermission) => {
    if (grantedPermission === allPermissions) {
      return requiredParts.length === 2;
    }

    if (grantedPermission.toLowerCase() === requiredPermission.toLowerCase()) {
      return true;
    }

    if (!grantedPermission.endsWith(':*')) {
      return false;
    }

    const grantedResource = grantedPermission.slice(0, -2);
    const grantedParts = grantedResource.split(':');

    return (
      requiredParts.length === grantedParts.length + 1 &&
      grantedResource.toLowerCase() === requiredParts.slice(0, grantedParts.length).join(':').toLowerCase()
    );
  });
}

export function hasAnyPermission(grantedPermissions: string[], requiredPermissions: string[]): boolean {
  return requiredPermissions.some((requiredPermission) => hasPermission(grantedPermissions, requiredPermission));
}

export function hasEveryPermission(grantedPermissions: string[], requiredPermissions: string[]): boolean {
  return requiredPermissions.every((requiredPermission) => hasPermission(grantedPermissions, requiredPermission));
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
```

- [ ] **Step 4: Wire Management Web TypeScript and Vite aliases**

Modify `src/OpenIdentityStack.ManagementWeb/tsconfig.app.json` paths:

```json
"paths": {
  "@/*": ["./src/*"],
  "@openidentitystack/permission-semantics": ["../frontend-packages/permission-semantics/src/index.ts"]
}
```

Modify `src/OpenIdentityStack.ManagementWeb/vite.config.ts` alias block:

```ts
alias: {
  '@': path.resolve(__dirname, './src'),
  '@openidentitystack/permission-semantics': path.resolve(__dirname, '../frontend-packages/permission-semantics/src/index.ts'),
},
```

- [ ] **Step 5: Run the test to verify it passes**

Run:

```powershell
cd src\OpenIdentityStack.ManagementWeb
npm test -- src/lib/permission-semantics-shared.test.ts
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/frontend-packages/permission-semantics/src/index.ts src/OpenIdentityStack.ManagementWeb/src/lib/permission-semantics-shared.test.ts src/OpenIdentityStack.ManagementWeb/tsconfig.app.json src/OpenIdentityStack.ManagementWeb/vite.config.ts
git commit -m "Add shared permission semantics module"
```

---

## Task 2: Convert Management Web To Compatibility Adapters

**Files:**
- Modify: `src/OpenIdentityStack.ManagementWeb/src/lib/auth-claims.ts`
- Modify: `src/OpenIdentityStack.ManagementWeb/src/lib/permissions.ts`
- Test: `src/OpenIdentityStack.ManagementWeb/src/lib/auth-claims.test.ts`
- Test: `src/OpenIdentityStack.ManagementWeb/src/lib/permissions.test.ts`

- [ ] **Step 1: Run existing adapter tests before changing code**

Run:

```powershell
cd src\OpenIdentityStack.ManagementWeb
npm test -- src/lib/auth-claims.test.ts src/lib/permissions.test.ts
```

Expected: PASS before the refactor. These tests define the compatibility interface that must remain green.

- [ ] **Step 2: Replace `auth-claims.ts` with a re-export adapter**

Modify `src/OpenIdentityStack.ManagementWeb/src/lib/auth-claims.ts`:

```ts
export { extractGrantedPermissions } from '@openidentitystack/permission-semantics';
```

- [ ] **Step 3: Replace `permissions.ts` with a re-export adapter**

Modify `src/OpenIdentityStack.ManagementWeb/src/lib/permissions.ts`:

```ts
export {
  allPermissions,
  hasAnyPermission,
  hasEveryPermission,
  hasPermission,
} from '@openidentitystack/permission-semantics';
```

- [ ] **Step 4: Run adapter tests**

Run:

```powershell
cd src\OpenIdentityStack.ManagementWeb
npm test -- src/lib/auth-claims.test.ts src/lib/permissions.test.ts src/lib/permission-semantics-shared.test.ts
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/OpenIdentityStack.ManagementWeb/src/lib/auth-claims.ts src/OpenIdentityStack.ManagementWeb/src/lib/permissions.ts
git commit -m "Use shared permission semantics in Management Web"
```

---

## Task 3: Align AdminWeb Permission Extraction

**Files:**
- Modify: `src/OpenIdentityStack.AdminWeb/src/features/auth/services/oidc-config.test.ts`
- Modify: `src/OpenIdentityStack.AdminWeb/src/features/auth/services/oidc-config.ts`
- Modify: `src/OpenIdentityStack.AdminWeb/tsconfig.app.json`
- Modify: `src/OpenIdentityStack.AdminWeb/vite.config.ts`

- [ ] **Step 1: Change AdminWeb extraction tests to the shared semantics**

Modify `src/OpenIdentityStack.AdminWeb/src/features/auth/services/oidc-config.test.ts`:

```ts
it('does not infer wildcard permission from admin role names', async () => {
  const { extractPermissions } = await importOidcConfig();

  expect(extractPermissions({ profile: { roles: ['user', 'admin'] } })).toEqual([]);
  expect(extractPermissions({ profile: { role: 'super-admin' } })).toEqual([]);
});

it('extracts and combines permissions from profile and access token claims', async () => {
  const { extractPermissions } = await importOidcConfig();
  const accessToken = jwtWithPayload({
    permissions: ['sessions:read'],
    scp: ['groups:read', 'openid'],
  });

  expect(
    extractPermissions({
      profile: {
        permission: ['users:read'],
        scope: 'openid profile applications:read',
      },
      access_token: accessToken,
    })
  ).toEqual(['users:read', 'applications:read', 'sessions:read', 'groups:read']);
});
```

Remove or replace the old test named `treats admin roles as wildcard permission`.

- [ ] **Step 2: Run the AdminWeb extraction tests to verify they fail**

Run:

```powershell
cd src\OpenIdentityStack.AdminWeb
npm test -- src/features/auth/services/oidc-config.test.ts
```

Expected: FAIL because AdminWeb still infers `admin`/`super-admin` as `*` and returns profile permissions before checking token permissions.

- [ ] **Step 3: Wire AdminWeb TypeScript and Vite aliases**

Modify `src/OpenIdentityStack.AdminWeb/tsconfig.app.json` paths:

```json
"paths": {
  "@/*": ["./src/*"],
  "@openidentitystack/permission-semantics": ["../frontend-packages/permission-semantics/src/index.ts"]
}
```

Modify `src/OpenIdentityStack.AdminWeb/vite.config.ts`:

```ts
resolve: {
  alias: {
    "@": path.resolve(__dirname, "./src"),
    "@openidentitystack/permission-semantics": path.resolve(__dirname, "../frontend-packages/permission-semantics/src/index.ts"),
  },
},
```

- [ ] **Step 4: Replace AdminWeb extraction implementation**

Modify the import section of `src/OpenIdentityStack.AdminWeb/src/features/auth/services/oidc-config.ts`:

```ts
import type { UserManagerSettings } from 'oidc-client-ts';
import { WebStorageStateStore } from 'oidc-client-ts';
import { extractGrantedPermissions } from '@openidentitystack/permission-semantics';
import { getRuntimeConfig } from '@/config/runtime-config';
```

Keep these local types/helpers:

```ts
type OidcClaims = Record<string, unknown>;
type OidcUserLike =
  | {
      profile?: unknown;
      access_token?: unknown;
    }
  | null
  | undefined;

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null;

const toClaims = (value: unknown): OidcClaims | null => {
  if (!isRecord(value)) {
    return null;
  }

  return value;
};

const getStringClaim = (claims: OidcClaims, claim: string): string | undefined => {
  const value = claims[claim];
  return typeof value === 'string' ? value : undefined;
};
```

Delete `toStringArray`; it is no longer needed.

Replace `extractPermissions` with:

```ts
export const extractPermissions = (user: OidcUserLike): string[] => {
  if (!user) {
    return [];
  }

  return extractGrantedPermissions({
    profile: user.profile,
    accessToken: typeof user.access_token === 'string' ? user.access_token : null,
  });
};
```

- [ ] **Step 5: Run AdminWeb extraction tests**

Run:

```powershell
cd src\OpenIdentityStack.AdminWeb
npm test -- src/features/auth/services/oidc-config.test.ts
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/OpenIdentityStack.AdminWeb/src/features/auth/services/oidc-config.ts src/OpenIdentityStack.AdminWeb/src/features/auth/services/oidc-config.test.ts src/OpenIdentityStack.AdminWeb/tsconfig.app.json src/OpenIdentityStack.AdminWeb/vite.config.ts
git commit -m "Align AdminWeb permission extraction"
```

---

## Task 4: Align AdminWeb Permission Matching

**Files:**
- Modify: `src/OpenIdentityStack.AdminWeb/src/features/auth/components/ProtectedRoute.test.tsx`
- Modify: `src/OpenIdentityStack.AdminWeb/src/features/auth/components/ProtectedRoute.tsx`
- Modify: `src/OpenIdentityStack.AdminWeb/src/features/auth/hooks/useAuth.test.tsx`
- Modify: `src/OpenIdentityStack.AdminWeb/src/features/auth/hooks/useAuth.ts`

- [ ] **Step 1: Add failing ProtectedRoute wildcard test**

Add this test to `src/OpenIdentityStack.AdminWeb/src/features/auth/components/ProtectedRoute.test.tsx`:

```tsx
it('renders children when a resource wildcard satisfies required permissions', () => {
  renderWithAuth(
    <ProtectedRoute requiredPermissions={['users:update']}>
      <div>Protected content</div>
    </ProtectedRoute>,
    {
      user: { sub: 'u1', email: 'a@b.com', name: 'Alice', permissions: ['users:*'] },
    }
  );

  expect(screen.getByText('Protected content')).toBeInTheDocument();
});
```

- [ ] **Step 2: Add failing hook wildcard test**

Update the `evaluates permission helper hooks from the authenticated user permissions` test in `src/OpenIdentityStack.AdminWeb/src/features/auth/hooks/useAuth.test.tsx`.

Use this `user.permissions` value:

```ts
permissions: ['users:*', 'roles:update'],
```

Use this consumer body:

```tsx
const PermissionConsumer = () => {
  const canReadUsers = usePermission('users:read');
  const canCreateUsers = usePermission('users:create');
  const canManageAny = usePermissions(['groups:read', 'roles:update']);
  const hasAll = useAllPermissions(['users:read', 'roles:update']);
  const missingOne = useAllPermissions(['users:read', 'groups:read']);

  return (
    <div>
      <span data-testid="can-read-users">{String(canReadUsers)}</span>
      <span data-testid="can-create-users">{String(canCreateUsers)}</span>
      <span data-testid="can-manage-any">{String(canManageAny)}</span>
      <span data-testid="has-all">{String(hasAll)}</span>
      <span data-testid="missing-one">{String(missingOne)}</span>
    </div>
  );
};
```

Keep the same assertions. `can-create-users` and `has-all` should now be `true` because `users:*` satisfies `users:create` and `users:read`.

- [ ] **Step 3: Run matching tests to verify they fail**

Run:

```powershell
cd src\OpenIdentityStack.AdminWeb
npm test -- src/features/auth/components/ProtectedRoute.test.tsx src/features/auth/hooks/useAuth.test.tsx
```

Expected: FAIL because AdminWeb still uses direct `includes` checks.

- [ ] **Step 4: Update ProtectedRoute to use shared matcher**

Modify `src/OpenIdentityStack.AdminWeb/src/features/auth/components/ProtectedRoute.tsx`.

Add import:

```ts
import { hasEveryPermission } from '@openidentitystack/permission-semantics';
```

Replace:

```ts
const hasRequiredPermissions = requiredPermissions.every(permission =>
  user?.permissions.includes(permission)
);
```

With:

```ts
const hasRequiredPermissions = hasEveryPermission(user?.permissions ?? [], requiredPermissions);
```

- [ ] **Step 5: Update `useAuth` permission hooks to use shared matchers**

Modify `src/OpenIdentityStack.AdminWeb/src/features/auth/hooks/useAuth.ts`.

Add import:

```ts
import {
  hasAnyPermission,
  hasEveryPermission,
  hasPermission,
} from '@openidentitystack/permission-semantics';
```

Replace `usePermission` return:

```ts
return hasPermission(user.permissions, permission);
```

Replace `usePermissions` return:

```ts
return hasAnyPermission(user.permissions, permissions);
```

Replace `useAllPermissions` return:

```ts
return hasEveryPermission(user.permissions, permissions);
```

- [ ] **Step 6: Run matching tests**

Run:

```powershell
cd src\OpenIdentityStack.AdminWeb
npm test -- src/features/auth/components/ProtectedRoute.test.tsx src/features/auth/hooks/useAuth.test.tsx
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add src/OpenIdentityStack.AdminWeb/src/features/auth/components/ProtectedRoute.tsx src/OpenIdentityStack.AdminWeb/src/features/auth/components/ProtectedRoute.test.tsx src/OpenIdentityStack.AdminWeb/src/features/auth/hooks/useAuth.ts src/OpenIdentityStack.AdminWeb/src/features/auth/hooks/useAuth.test.tsx
git commit -m "Use shared permission matching in AdminWeb"
```

---

## Task 5: Verify Cross-UI Permission Semantics

**Files:**
- Verify only unless failures require focused fixes.

- [ ] **Step 1: Run focused Management Web tests**

Run:

```powershell
cd src\OpenIdentityStack.ManagementWeb
npm test -- src/lib/auth-claims.test.ts src/lib/permissions.test.ts src/lib/permission-semantics-shared.test.ts src/routes/AppRoutes.test.tsx
```

Expected: PASS.

- [ ] **Step 2: Run focused AdminWeb tests**

Run:

```powershell
cd src\OpenIdentityStack.AdminWeb
npm test -- src/features/auth/services/oidc-config.test.ts src/features/auth/components/ProtectedRoute.test.tsx src/features/auth/hooks/useAuth.test.tsx
```

Expected: PASS.

- [ ] **Step 3: Run type checks**

Run:

```powershell
cd src\OpenIdentityStack.ManagementWeb
npm run type-check
```

Expected: PASS.

Run:

```powershell
cd src\OpenIdentityStack.AdminWeb
npm run type-check
```

Expected: PASS.

- [ ] **Step 4: Check git status**

Run:

```powershell
git status --short
```

Expected: only intentional files are modified.

- [ ] **Step 5: Commit final verification adjustments if needed**

If any focused fixes were required:

```powershell
git add <fixed-files>
git commit -m "Verify shared permission semantics"
```

If no fixes were required, do not create an empty commit.

---

## Self-Review

Spec coverage:

- Shared extraction semantics: Task 1.
- Shared matching semantics: Task 1.
- Management Web adapters: Task 2.
- AdminWeb extraction alignment and removal of role-name wildcard inference: Task 3.
- AdminWeb guard and hook matching alignment: Task 4.
- Cross-UI verification: Task 5.

Placeholder scan:

- No forbidden placeholder markers or vague test instructions are present.

Type consistency:

- The shared module exports `extractGrantedPermissions`, `hasPermission`, `hasAnyPermission`, `hasEveryPermission`, and `allPermissions`.
- UI adapters and tests consistently import `@openidentitystack/permission-semantics`.
- AdminWeb keeps public `extractPermissions` and `extractDisplayName` exports unchanged.
