# OpenIdentityStack

Canonical glossary for product language used in this repository.

## Language

**Management Web**:
The sole supported browser frontend for OpenIdentityStack operator workflows, implemented at `src/OpenIdentityStack.ManagementWeb`.
_Avoid_: Legacy frontend terminology, dual-frontend rollout, parallel admin UI

**Management Web Client**:
The OpenID Connect public client used by Management Web, with client ID `management-web-client`.
_Avoid_: Shared SPA client, legacy admin client

**Admin API**:
The backend management API surface under `/api/admin/*` consumed by Management Web.
_Avoid_: Frontend-specific API clone, management BFF

**Applications Model**:
The unified software-registration model exposed at `/api/admin/applications`. It replaces legacy Clients and Service Accounts in operator-facing flows.
_Avoid_: Reintroducing `/clients` or `/service-accounts` UI concepts

**Theme Preference**:
The user's explicit Management Web appearance choice (`light`, `dark`, or `system`) stored in browser local storage.
_Avoid_: Server-side theme persistence, role-driven theme switching

**Permission Semantics**:
Frontend route and action gating is derived from concrete permission claims; backend authorization remains authoritative.
_Avoid_: Inferring privilege from role names alone

**Current User**:
The authenticated Management Web user as represented to the frontend by the API, including their effective permission snapshot.
_Avoid_: Managed user record, token payload, live permission recalculation

**Management Web Availability**:
Management Web is started by default in the local Aspire composition and is the only interactive frontend resource.
_Avoid_: Dual-frontend local startup, legacy UI fallback assumptions

**Management Web Verification Strategy**:
Vitest covers frontend units/components and `tests/OpenIdentityStack.ManagementWeb.E2ETests` provides Playwright-based end-to-end coverage.
_Avoid_: Legacy frontend verification requirements

**Runtime Configuration Model**:
Management Web receives `VITE_OIDC_AUTHORITY`, `VITE_API_BASE_URL`, and `VITE_OIDC_CLIENT_ID` from AppHost in local development.
_Avoid_: Build-time-only runtime binding

**Frontend Workspace**:
The repository contains the active Management Web frontend plus shared frontend packages under `src/frontend-packages`.
_Avoid_: Treating removed frontend code as active product surface

## Example dialogue

Dev: "Should I add this operator page to another frontend too?"
Domain expert: "No. Management Web is the only supported frontend, so implement it there and keep the Admin API aligned."
