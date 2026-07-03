# Management Web Contract

## Purpose

Defines the expected operator-facing behavior for ManagementWeb after the Management Web parity expansion.

## Contract Surface

- **Entry point**: Separate hostname from Management Web.
- **Authentication**: Reuses the active identity-provider session when present.
- **Authorization**: Uses normalized frontend permission checks while backend policy remains authoritative. ManagementWeb consumes granular permissions from `permission`, `permissions`, `scope`, and `scp` claims in both OIDC profile data and access-token payloads. It must not infer authorization from role names alone.
- **Theme behavior**: Supports light, dark, and system appearance.
- **Visual system**: Uses Mantine components and patterns.
- **Behavior baseline**: Management Web behavior is the parity baseline unless this contract states an explicit deviation.
- **Applications**: Uses only `/api/admin/applications`; no Clients or Service Accounts UI is exposed.
- **Audit**: Adds ManagementWeb-only audit visibility backed by `GET /api/admin/audit-entries`.

## Routes

ManagementWeb preserves Management Web-compatible route paths where the domain still exists:

- `/`
- `/users`
- `/users/create`
- `/users/:id`
- `/users/:id/edit`
- `/roles`
- `/roles/new`
- `/roles/:id`
- `/groups`
- `/groups/new`
- `/groups/:id`
- `/groups/:id/edit`
- `/applications`
- `/applications/new`
- `/applications/:id`
- `/applications/:id/edit`
- `/application-permissions`
- `/application-permissions/new`
- `/application-permissions/:id`
- `/sessions`
- `/sessions/:id`
- `/providers`
- `/providers/new`
- `/providers/:id`
- `/providers/:id/edit`
- `/settings`
- `/audit-entries`

ManagementWeb must not add `/clients` or `/service-accounts`.

## Audit Endpoint

### `GET /api/admin/audit-entries`

Authorization: `audit-logs:read`

Query parameters:

- `page`
- `pageSize`
- `from`
- `to`
- `userId`
- `action`
- `entityType`
- `entityId`
- `search`

Response shape:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "timestamp": "2026-06-01T12:00:00Z",
      "userId": "admin-user",
      "action": "Application.Created",
      "entityType": "Application",
      "entityId": "00000000-0000-0000-0000-000000000000",
      "details": "Optional detail text",
      "beforeState": null,
      "afterState": "{\"status\":\"Active\"}"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

## Acceptance Expectations

1. An authenticated operator can open ManagementWeb without a second login when already signed in elsewhere.
2. The operator can switch appearance mode and see the choice persist on return.
3. Applications behave like Management Web Applications and never fall back to Clients or Service Accounts.
4. Each completed vertical slice has unit/component/API coverage and meaningful E2E coverage.
5. Permission-gated actions are hidden or disabled consistently from granular permission grants, and backend authorization failures are surfaced clearly.
6. Audit entries can be filtered, paged, and expanded without a second detail endpoint.

