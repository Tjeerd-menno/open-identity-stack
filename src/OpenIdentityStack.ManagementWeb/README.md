# OpenIdentityStack Management Web

Mantine-based management UI for OpenIdentityStack. It runs side by side with the existing AdminWeb during rollout.

## Local development

```powershell
npm install
npm run dev
```

The Aspire AppHost configures `VITE_API_BASE_URL`, `VITE_OIDC_AUTHORITY`, and `VITE_OIDC_CLIENT_ID` for local runs.
