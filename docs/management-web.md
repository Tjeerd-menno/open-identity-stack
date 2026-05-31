# Management Web

Management Web is the new Mantine-based OpenIdentityStack operator UI. It runs side by side with AdminWeb while domains are migrated incrementally.

## Operator access

- Local Aspire resource: `managementweb`
- Local development port: `http://localhost:5176`
- OIDC client ID: `management-web-client`
- Admin API authority and base URL are supplied by the AppHost during local runs.

The first production slice focuses on Users. Operators can list users, inspect details, update display names, disable accounts, and assign existing roles without leaving Management Web.

## Appearance

Operators can choose light, dark, or system appearance. The preference is stored only in browser local storage under `openidentitystack.management.theme`.

## Rollout posture

AdminWeb remains available on its existing host while Management Web is introduced on a separate host. Backend authorization remains authoritative for both UIs. If rollout issues appear, disable Management Web by setting `OPENIDENTITYSTACK_ENABLE_MANAGEMENTWEB=false`; AdminWeb continues to serve operators.
