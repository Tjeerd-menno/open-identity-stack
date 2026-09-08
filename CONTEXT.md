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

**Account Linking**:
The explicit association of an upstream identity with an existing local user, authorized through proof of control of that local account.
_Avoid_: Email matching as identity proof, automatic email-based account merge

**Local Administrative Disablement**:
A local operator's decision to deny a user access until that decision is explicitly reversed locally.
_Avoid_: Upstream authentication as reactivation, provisioning updates overriding local disablement

**Quarantined Identity Link**:
An existing association between an upstream identity and a local user that is retained but cannot authenticate the user because independent evidence of legitimate association is missing.
_Avoid_: Deleting disputed links as proof of resolution, login through the disputed link as ownership evidence

**Administrative Access Entitlement**:
An explicit approval for a client application to request access to the Admin API, separate from the user's administrative permissions.
_Avoid_: Administrative access inherited from the signing-in user, ordinary application registration as administrative approval

**Client Application**:
A registered application that requests access on behalf of a user or through its own assigned machine permissions.
_Avoid_: Treating a client identifier as a resource or permission namespace

**Protected Resource**:
An API that receives access tokens intended for its resource identity and exposes operations governed by explicitly mapped permission namespaces.
_Avoid_: Resource identity inferred from an OAuth client identifier, shared administrative and business audience

**Permission Namespace**:
A named set of operations explicitly associated with a protected resource.
_Avoid_: Permission namespace inferred from client identity, OAuth scope as an interchangeable permission

**Client Permission Ceiling**:
The explicitly permitted operations a client may request for a protected resource; delegated access is also limited by the user's permissions.
_Avoid_: Inheriting every permission of the signing-in user, ordinary application editing as authority to expand administrative access

**All-Permissions Grant**:
An explicit grant of every platform permission, including future permissions, reserved for controlled bootstrap and emergency administration.
_Avoid_: Administrator privileges inferred from role names, routine operator access

**Fresh Administrative Authentication**:
Recent authentication of the human administrator approving a privileged change, measured from the actual authentication event.
_Avoid_: Cookie renewal as reauthentication, token refresh as authentication proof, machine credentials as human approval

**Emergency Administrator**:
An independently accessible administrator retained for controlled recovery of platform access.
_Avoid_: Routine operator account, automatic reactivation through seeding, identity-proof bypass

**Provider Email Verification Trust**:
An explicit policy allowing an upstream provider to attest email ownership; this trust does not authorize account linking.
_Avoid_: Trust implied by provider registration, verified email as account identity

**Email Verification Provenance**:
The evidence establishing whether an email address was verified and by whom, independent of local account activation.
_Avoid_: Active account as verified email, missing verification evidence interpreted as true

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
