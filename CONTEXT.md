# OpenIdentityStack

Canonical glossary for product language used in this repository.

## Language

**Management Web**:
The new production-grade management frontend for OpenIdentityStack, implemented as a separate app at `src/OpenIdentityStack.ManagementWeb` and operated in parallel with AdminWeb.
_Avoid_: New AdminWeb, Mantine admin app, v2 admin UI

**AdminWeb**:
The existing React/Vite admin frontend at `src/OpenIdentityStack.AdminWeb` that remains an actively developed management UI alongside Management Web.
_Avoid_: Legacy-only frontend, deprecated admin UI

**Management Web Client**:
The dedicated OpenID Connect client registration used only by Management Web, with its own client ID, redirect URIs, and scope policy.
_Avoid_: Reusing AdminWeb client, shared SPA client

**Admin API**:
The existing backend management API surface (`/api/admin/*`) consumed directly by both Management Web and AdminWeb during migration.
_Avoid_: Management BFF API, frontend-specific API clone

**Theme Preference**:
The user's explicit appearance choice (`light`, `dark`, or `system`) for Management Web, where first load follows system settings and later loads use the saved preference.
_Avoid_: Hardcoded dark mode, session-only theme

**Users Vertical Slice**:
The first migration phase for Management Web: production-ready user-management workflows delivered end-to-end before other administration domains move.
_Avoid_: Big-bang parity migration, shell-only launch

**Parallel UI Rollout**:
The migration strategy where AdminWeb and Management Web run side by side on separate endpoints, with AdminWeb remaining the default entry point until explicit cutover.
_Avoid_: Immediate replacement, hidden legacy access

**Mantine-First UI**:
The frontend composition rule for Management Web where Mantine provides the primary components, layout primitives, and theming foundation.
_Avoid_: Ad-hoc component system, mixed UI foundations

**Production-Grade Management UI**:
The quality bar for Management Web UX and interaction quality: accessible-by-default interactions, consistent keyboard operability, and reliable layout behavior on common administrator screen sizes; it does not by itself imply rollout-risk controls.
_Avoid_: Prototype-grade admin UI, demo-only polish

**Default Cutover Rule**:
Management Web becomes the default operator UI immediately when the Users Vertical Slice is released.
_Avoid_: Telemetry-gated cutover, extended dual-default period

**Rollback Policy**:
If cutover causes issues, restoring AdminWeb as default may require deployment-time or configuration-change operations rather than an instant runtime toggle.
_Avoid_: Immediate runtime rollback switch

**Global Cutover**:
When the cutover condition is met, Management Web is promoted for all operators at once rather than through staged cohort rollout.
_Avoid_: Canary operator rollout

**Parity Policy**:
Feature parity between AdminWeb and Management Web is a best-effort objective and not a hard release gate.
_Avoid_: Mandatory parity gating

**UI Quality Asymmetry**:
Management Web carries the premium UX and polish target, while AdminWeb is allowed to remain functionally adequate without matching the same design-quality bar.
_Avoid_: Uniform UX standard across both UIs

**Management Web Availability**:
When deployed, Management Web is expected to be started and exposed by default rather than gated behind an explicit environment-level enable flag.
_Avoid_: Default-off launch gating

**Permission Semantics**:
AdminWeb and Management Web interpret and enforce the same permission/claim model, with authorization behavior anchored to backend policy decisions.
_Avoid_: UI-specific permission rules

**Shared Admin API Client**:
A shared typed frontend contract/client layer consumed by both AdminWeb and Management Web to keep backend integration behavior aligned.
_Avoid_: Duplicated per-UI API client stacks

**Frontend Workspace**:
A repo-root npm workspace topology that hosts AdminWeb, ManagementWeb, and shared frontend packages under one dependency-management boundary.
_Avoid_: Fully isolated frontend package management

**Dual-UI Verification Strategy**:
AdminWeb keeps its current verification coverage, Management Web adds dedicated end-to-end coverage, and shared frontend API contracts are validated in shared tests.
_Avoid_: Single-UI-only verification

**Users Slice Scope**:
The initial Management Web users slice includes core user lifecycle operations and basic role assignment so operators can complete primary user-administration flows without switching domains.
_Avoid_: User CRUD without role assignment

**High-Risk Cutover Posture**:
The rollout posture where Management Web can become the default for all operators without staged exposure and without an instant runtime fallback to AdminWeb.
_Avoid_: Guardrailed rollout posture

**Dual-Host UI Topology**:
AdminWeb and Management Web are exposed on separate hostnames per environment instead of being path-segmented under one host.
_Avoid_: Single-host path-split UI routing

**Cross-UI SSO**:
Operators should move between AdminWeb and Management Web without re-authenticating when an identity-provider session is already active.
_Avoid_: Per-UI isolated login experience

**Independent Theme Ownership**:
AdminWeb and Management Web maintain separate theming implementations and are not required to share a common token source.
_Avoid_: Shared cross-UI design token package

**Independent UI Deployables**:
AdminWeb and Management Web are released as separate deployable artifacts so each UI can be promoted, rolled back, and versioned independently.
_Avoid_: Coupled dual-UI deployment artifact

**Per-UI Observability**:
Each management UI has its own telemetry, alerting, and operational health signals rather than being aggregated into one frontend bucket.
_Avoid_: Combined dual-UI operational dashboard only

**AdminWeb End-State**:
AdminWeb remains active during transition but is intended to be decommissioned once Management Web reaches sufficient domain and operational maturity.
_Avoid_: Permanent dual-UI operation as default plan

**AdminWeb Decommission Gate**:
AdminWeb can be retired after Management Web reaches complete management-domain coverage, satisfies the production-grade quality baseline, and demonstrates stable production operation for 30 days.
_Avoid_: Date-only decommission target

**Browser Support Policy**:
Management Web officially targets current and previous stable Chrome/Edge releases, with Firefox treated as best-effort compatibility.
_Avoid_: Legacy browser guarantees

**Phase-1 User Operation Mode**:
The first Management Web users slice focuses on single-user workflows and excludes bulk user operations until later phases.
_Avoid_: Bulk user operations in phase 1

**Phase-1 Audit Visibility**:
Management Web phase 1 does not include dedicated in-UI audit history views and relies on existing backend audit capability outside the new UI.
_Avoid_: Mandatory audit-history panel in phase 1

**Phase-1 Localization Posture**:
Management Web phase 1 includes localization plumbing but ships with English content only.
_Avoid_: Hardcoded no-i18n architecture, full multilingual phase-1 obligation

**Operational Error UX**:
Management Web surfaces recoverable failures close to the operator action context and reserves global error boundaries for application-breaking failures.
_Avoid_: Global-only error reporting for routine operation failures

**Session Continuity**:
Management Web should attempt silent token renewal before requiring interactive re-authentication to keep operator workflows uninterrupted when possible.
_Avoid_: Immediate login redirect on first token expiry

**Domain Navigation Skeleton**:
Management Web phase 1 includes navigation placeholders for all management domains even when only the users domain is fully functional.
_Avoid_: Users-only navigation information architecture

**Phase-1 UX Performance Target**:
Management Web phase 1 targets p95 sub-2-second initial Users-page render and p95 sub-300ms response for common table interactions.
_Avoid_: Unbounded phase-1 interaction latency

**Accessibility Release Gate**:
Management Web release readiness requires automated accessibility checks and targeted manual accessibility smoke validation.
_Avoid_: Accessibility verification through only one validation mode

**Runtime Configuration Model**:
Management Web uses environment-driven runtime configuration resolution aligned with existing AdminWeb operational configuration patterns.
_Avoid_: Build-time-only environment coupling

**Post-Users Migration Sequence**:
After the users slice, Management Web domain migration proceeds in this order: Groups, then Roles, then Service Accounts.
_Avoid_: Assumed roles-first migration order

**Phase-1 Role Assignment Boundary**:
Users slice supports assigning users to existing roles, while role definition and permission composition remain in the later Roles slice.
_Avoid_: Full role-management domain within phase-1 users delivery

**API Evolution Posture**:
Admin API evolution may include non-versioned breaking changes, with frontend clients expected to adapt as part of coordinated delivery.
_Avoid_: Strict backward-compatibility-first API policy

**Unified Release Train**:
Breaking Admin API changes require synchronized release orchestration across API, shared frontend client, AdminWeb, and Management Web.
_Avoid_: Independent per-component release cadence for breaking changes

**Release Train Ownership**:
One designated cross-UI release owner has final accountability for approving and coordinating breaking-change release trains.
_Avoid_: Diffuse shared ownership without final decision authority

**Silent UI Transition**:
Default-UI transitions do not require mandatory in-product migration notices or temporary cross-link messaging.
_Avoid_: Forced transition banners

**Unannounced Cutover Policy**:
Management UI cutovers are permitted without mandated operator-facing communication artifacts.
_Avoid_: Required pre-cutover release communication

**Per-UI E2E Test Projects**:
Each management frontend has its own dedicated end-to-end test project, fixture, and OIDC test-client configuration.
_Avoid_: Shared cross-UI E2E project assumptions

**Shared Client Package Location**:
The shared frontend Admin API client package is hosted in `src/frontend-packages/admin-api-client` as an internal workspace package.
_Avoid_: UI-local relative-import shared client implementations

**Root Frontend Orchestration**:
The repository root provides orchestrating frontend scripts that run shared package and multi-UI build/lint/test flows consistently.
_Avoid_: Purely manual per-frontend execution flow

**ManagementWeb Aspire Resource**:
AppHost registers a dedicated `managementweb` JavaScript app resource with its own stable development endpoint configuration.
_Avoid_: Reusing `adminweb` resource identity for the new UI

**Dual-Client Test Seeding**:
Test seeding utilities provision both AdminWeb and Management Web OAuth public clients to keep UI test suites independent from per-suite client-registration assumptions.
_Avoid_: Single-client-only shared seed baseline

**Frontend CI Isolation Policy**:
CI enforces separate required checks for each UI and a distinct required check for shared frontend packages.
_Avoid_: One monolithic frontend required check

## Example dialogue

Dev: "Should I add this user-management page to AdminWeb?"
Domain expert: "Yes, if the same capability is also implemented in Management Web so both UIs stay in feature parity."
