# OpenIdentityStack Management Web

Mantine-based management UI for OpenIdentityStack. It runs side by side with the existing AdminWeb during rollout.

## Scope

Management Web exposes Overview, Users, Roles, Groups, Applications, Permissions, Sessions, Identity providers, Settings, and Audit. It intentionally does not expose Clients or Service Accounts. Applications uses the consolidated `/api/admin/applications` API only.

## Local development

```powershell
npm install
npm run dev
```

The Aspire AppHost configures `VITE_API_BASE_URL`, `VITE_OIDC_AUTHORITY`, and `VITE_OIDC_CLIENT_ID` for local runs.

## Validation

```powershell
npm run build
npm run lint
npm test
npm run type-check
```

Management Web E2E coverage is in `tests/OpenIdentityStack.ManagementWeb.E2ETests` and uses .NET/xUnit Playwright tests.
