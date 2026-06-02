# Management Web

Management Web is the new Mantine-based OpenIdentityStack operator UI. It runs side by side with AdminWeb while domains are migrated incrementally.

## Current parity scope

Management Web now covers the retained operator domains:

- Overview
- Users
- Roles
- Groups
- Applications
- Permissions
- Sessions
- Identity providers
- Settings
- Audit

The navigation intentionally excludes Clients and Service Accounts. Application-like administration is consolidated into Applications and uses `/api/admin/applications`.

## Operator access

- Local Aspire resource: `managementweb`
- Local development port: `http://localhost:5176`
- OIDC client ID: `management-web-client`
- Admin API authority and base URL are supplied by the AppHost during local runs.

Operators land on Overview, which summarizes available sections from their concrete permission grants and provides quick links to the retained domains.

## Audit

Audit is read-only in Management Web v1. Operators with `audit-logs:read` can open `/audit-entries`, page through newest-first audit records, filter by date range, user id, action, entity type, entity id, and search text, and expand a row to inspect `details`, `beforeState`, and `afterState`.

The UI uses `GET /api/admin/audit-entries`; there is no Management Web audit write endpoint.

## Authorization

Management Web uses shared permission helpers for route and action gates, but backend authorization remains authoritative. The frontend consumes concrete permission grants from `permission`, `permissions`, `scope`, and `scp` claims. Role names such as `admin` or `super-admin` are not treated as frontend wildcard grants.

## Appearance

Operators can choose light, dark, or system appearance. The preference is stored only in browser local storage under `openidentitystack.management.theme`.

## Rollout posture

AdminWeb remains available on its existing host while Management Web is introduced on a separate host. Backend authorization remains authoritative for both UIs. If rollout issues appear, disable Management Web by setting `OPENIDENTITYSTACK_ENABLE_MANAGEMENTWEB=false`; AdminWeb continues to serve operators.

AdminWeb decommission should wait until:

- all retained Management Web routes pass slice-level component and .NET/xUnit Playwright E2E coverage;
- Applications, Users, Audit, and Overview workflows pass production-like smoke validation;
- no Management Web code or navigation exposes Clients or Service Accounts;
- operations has rollback and hostname routing documented for both UIs.
