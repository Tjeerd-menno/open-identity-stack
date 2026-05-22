<!--
Sync Impact Report
Version change: template placeholders -> 1.0.0
Modified principles:
- Template principle 1 -> I. Security by Design
- Template principle 2 -> II. Test-First, Risk-Based Verification
- Template principle 3 -> III. Layered Architecture with Vertical Feature Slices
- Template principle 4 -> IV. Simplicity and Dependency Discipline
- Template principle 5 -> V. Operational Reliability and Observability
- Added VI. User-Facing and API Consistency
Added sections:
- Technology and Package Constraints
- Development Workflow and Quality Gates
Removed sections:
- None
Templates requiring updates:
- ✅ updated: .specify/templates/plan-template.md
- ✅ updated: .specify/templates/spec-template.md
- ✅ updated: .specify/templates/tasks-template.md
- ⚠ pending: .specify/templates/commands/*.md (directory not present in this installation)
Follow-up TODOs:
- None
-->

# OpenIdentityStack Constitution

## Core Principles

### I. Security by Design

OpenIdentityStack is an identity and access management product. Authentication,
authorization, secrets, certificates, token handling, session management,
auditability, and safe error behavior are product correctness requirements.
Every feature that touches identity, access, administration, configuration, or
runtime behavior MUST define its security impact, permission boundaries, audit
events for security-relevant operations, and failure modes.

Secrets MUST NOT be stored or logged in plaintext. User-facing and API errors
MUST NOT enable account enumeration or disclose sensitive operational details.
Privileged operations MUST be authenticated, authorized, and auditable.
The rationale is direct: a working identity system that fails these rules is not
safe to operate.

### II. Test-First, Risk-Based Verification

Tests MUST be planned before implementation. Behavior changes MUST include test
tasks before implementation tasks unless the plan documents why the change is a
low-risk documentation or mechanical update. Domain and application logic MUST
have focused unit tests. API behavior MUST have integration tests. Public API
shape changes MUST have contract tests when the contract is externally visible
or consumed by AdminWeb or third-party clients. AdminWeb workflows MUST use
Vitest, Playwright, or both based on user impact and risk.

Generated tasks MUST identify which tests are expected to fail before
implementation and which validation commands prove the change. This keeps
verification proportional to risk while preserving fast feedback.

### III. Layered Architecture with Vertical Feature Slices

Backend changes MUST preserve the existing Clean/Hexagonal layering. Domain
contains business rules and domain types. Application contains use cases,
queries, commands, DTOs, and ports. Infrastructure contains persistence,
OpenIddict, external provider, audit, and other adapter implementations. Api
contains HTTP and UI adapters. AppHost orchestrates the local Aspire runtime.

Features SHOULD be organized by domain slice, such as Users, Groups, Roles,
Sessions, ServiceAccounts, Federation, Clients, and ServicePermissions.
Dependencies MUST point inward. Domain code MUST NOT depend on ASP.NET Core,
EF Core, OpenIddict, React, or other adapter concerns. The rationale is to keep
business behavior testable, explicit, and isolated from delivery mechanisms.

### IV. Simplicity and Dependency Discipline

Implementations MUST prefer direct services and use cases, explicit DTO mapping,
System.Text.Json, Microsoft OpenAPI plus Scalar, and existing repository
patterns. The following packages are disallowed unless a future constitutional
amendment explicitly allows them: Newtonsoft.Json, Swashbuckle or Swagger
packages, AutoMapper, and MediatR.

New packages MUST be justified by irreplaceable value, maintenance health,
security posture, license compatibility, and compatibility with .NET 10 or the
AdminWeb stack. New abstractions MUST remove real complexity, protect a stable
boundary, or match an established local pattern.

### V. Operational Reliability and Observability

Features that affect runtime behavior MUST include health, logging, diagnostics,
configuration, migration, deployment, and rollback considerations proportional
to risk. Production paths MUST preserve PostgreSQL persistence, data-protection
storage, signing and encryption key safety, reverse-proxy behavior, and secure
configuration. Local Aspire development ergonomics MUST remain usable.

Security-relevant and operationally significant actions MUST be observable
without exposing secrets or sensitive personal data. Migrations, startup
behavior, certificates, CORS, forwarded headers, and service dependencies MUST
be planned and documented when affected.

### VI. User-Facing and API Consistency

Admin APIs MUST use consistent resource shapes, validation, pagination,
Problem Details-style errors, authorization behavior, and OpenAPI documentation.
AdminWeb changes MUST follow the existing React/Vite feature-folder structure,
use established UI components and hooks, and keep workflows accessible,
predictable, and testable.

User-facing behavior MUST be specified with independently testable scenarios and
measurable outcomes. This keeps API consumers, administrators, and operators
from having to relearn behavior from feature to feature.

## Technology and Package Constraints

OpenIdentityStack uses .NET 10, OpenIddict, ASP.NET Core, .NET Aspire,
EF Core with PostgreSQL, React, Vite, TypeScript, Vitest, and Playwright.
Central package management is enabled through Directory.Packages.props.
Warnings are treated as errors, analyzers are enabled, nullable reference types
are enabled, and .editorconfig is authoritative for code style.

Backend features MUST fit the existing solution structure under src/ and tests/.
AdminWeb features MUST fit the existing React/Vite structure under
src/OpenIdentityStack.AdminWeb. Documentation belongs under docs/ for product
and operations guidance, and specs/ for feature design artifacts.

Disallowed packages:

- Newtonsoft.Json: use System.Text.Json.
- Swashbuckle or Swagger packages: use Microsoft.AspNetCore.OpenApi and Scalar.
- AutoMapper: use explicit mapping.
- MediatR: use direct use-case, service, query, or command handler injection.

Any exception requires a constitutional amendment, rationale, migration impact,
and template updates.

## Development Workflow and Quality Gates

Specs MUST define prioritized user scenarios, independently testable acceptance
criteria, measurable success criteria, security impact, test strategy,
performance or operational expectations, documentation impact, and assumptions.

Plans MUST run the Constitution Check before Phase 0 research and again after
Phase 1 design. A passing check MUST cover the six core principles, technology
and package constraints, testing strategy, documentation impact, and validation
commands. Any violation MUST be listed in Complexity Tracking with the reason,
the simpler alternative considered, and the migration or risk impact.

Tasks MUST preserve independent user-story delivery, include exact file paths,
place required failing tests before implementation, include security and
observability work when relevant, and include final validation commands. Plans
and tasks MUST call out updates to docs/, deploy/, AppHost, database migrations,
or AdminWeb screenshots when behavior changes.

## Governance

This constitution supersedes conflicting repository practice for Spec Kit-driven
work except explicit user instruction. AGENTS.md remains the runtime guidance
for repository commands, structure, coding style, and collaboration details.

Amendments require a stated rationale, security and migration impact assessment,
updated dependent templates, and a Sync Impact Report at the top of this file.
Version changes follow semantic versioning:

- MAJOR: backward-incompatible governance changes, removed principles, or
  redefined principles.
- MINOR: new principles, new required sections, or materially expanded gates.
- PATCH: clarifications, wording fixes, or non-semantic refinements.

Every generated spec, plan, and task list MUST be reviewed for constitutional
compliance. Pull requests SHOULD summarize validation performed and call out
security, documentation, deployment, or operational impacts when applicable.

**Version**: 1.0.0 | **Ratified**: 2026-05-22 | **Last Amended**: 2026-05-22
