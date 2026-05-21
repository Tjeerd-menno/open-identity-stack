# Implementation Plan: React Admin Web App with Shadcn UI

**Branch**: `copilot/add-react-web-app-shadcn-ui` | **Date**: 2026-01-18 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/copilot/add-react-web-app-shadcn-ui/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Create a modern React-based administrative web application with Shadcn UI component library to provide a comprehensive interface for managing users, roles, groups, service accounts, sessions, and identity providers. The application will authenticate via OAuth2/OIDC against the existing OpenIddict server, consume existing admin API endpoints, and be orchestrated by the .NET Aspire AppHost alongside the existing API and PostgreSQL services.

## Technical Context

**Language/Version**: TypeScript 6+, React 19+, Node.js 24+ LTS  
**Primary Dependencies**: 
  - Vite 8+ (build tool and dev server)
  - Shadcn UI (component library based on Radix UI + Tailwind CSS)
  - TanStack Query v5 (API state management)
  - React Router v7 (client-side routing)
  - @auth/core or oidc-client-ts (OAuth2/OIDC authentication)
  - Zod (runtime validation and TypeScript schema)
  
**Storage**: N/A (stateless frontend, API handles data)  
**Testing**: 
  - Vitest (unit tests for components and utilities)
  - React Testing Library (component testing)
  - Playwright (E2E tests for critical flows)
  
**Target Platform**: Modern browsers (Chrome, Firefox, Safari, Edge - last 2 versions)  
**Project Type**: Web (frontend single-page application)  
**Performance Goals**: 
  - Initial page load: < 3 seconds
  - Time to Interactive (TTI): < 2 seconds
  - API call response feedback: < 100ms
  
**Constraints**: 
  - Must integrate with .NET Aspire 13.3.x AppHost
  - Must consume existing admin API without modifications
  - Must authenticate via existing OpenIddict 7.5.0 server
  - Admin API requires OpenIddict JWT access tokens with specific permissions
  
**Scale/Scope**: 
  - 6 main admin modules (users, roles, groups, service accounts, sessions, providers)
  - ~15-20 views (list, detail, create, edit per module)
  - Support for 1-100 concurrent admin users
  - Target dataset sizes: 1K-100K users, 10-1K roles/groups

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### I. Test-First Development (NON-NEGOTIABLE)
**Status**: ✅ **PASS**
- **Plan**: Unit tests for React components using Vitest + React Testing Library
- **Plan**: Integration tests for API client layer
- **Plan**: E2E tests for critical admin flows (login, user CRUD) using Playwright
- **Approach**: Write tests for each component/feature before implementation (TDD)
- **Red-Green-Refactor**: Enforced in development workflow

### II. Clean Code Standards
**Status**: ✅ **PASS**
- **Linting**: ESLint with TypeScript rules + React best practices
- **Formatting**: Prettier for consistent code formatting
- **Structure**: Functional components, custom hooks for reusable logic
- **Naming**: Intention-revealing names (e.g., `useAuthenticatedUser`, `UserListTable`)
- **DRY**: Shared components in `/src/components`, utilities in `/src/lib`

### III. Vertical Slice Architecture
**Status**: ⚠️ **CONDITIONAL PASS**
- **Frontend Structure**: Feature-based organization aligned with admin modules
  ```
  src/features/
    ├── users/        (User management slice)
    ├── roles/        (Role management slice)
    ├── groups/       (Group management slice)
    ├── service-accounts/
    ├── sessions/
    └── providers/
  ```
- **Justification**: Frontend architecture differs from backend vertical slices but maintains feature isolation. Each feature contains its own components, hooks, and API client calls.

### IV. Security by Design
**Status**: ✅ **PASS**
- **Input Validation**: Client-side validation with Zod schemas before API calls
- **Authentication**: OAuth2/OIDC with PKCE flow (secure for SPAs)
- **Token Storage**: Secure token management (httpOnly cookies or secure memory storage)
- **Authorization**: Permission checks before rendering admin actions
- **XSS Protection**: React's built-in escaping + Content Security Policy
- **CSRF Protection**: SameSite cookies + CORS configuration
- **No Secrets**: Environment variables for OIDC configuration (client_id, issuer)

### V. User Experience Consistency
**Status**: ✅ **PASS**
- **Design System**: Shadcn UI provides consistent component library
- **Loading States**: Loading indicators within 100ms for all async operations
- **Error Handling**: User-friendly error messages with actionable guidance
- **Navigation**: Consistent sidebar navigation across all modules
- **Accessibility**: WCAG 2.1 AA compliance using Radix UI primitives
- **Responsive**: Mobile-first Tailwind CSS breakpoints

### VI. Performance Requirements
**Status**: ✅ **PASS**
- **API Response**: 
  - TanStack Query for request deduplication and caching
  - Optimistic updates for mutations
  - Background data refetching
- **Bundle Optimization**:
  - Vite code splitting (route-based)
  - Lazy loading for feature modules
  - Tree-shaking for unused code
- **Target Metrics**:
  - P50 page load: ≤2s
  - P95 page load: ≤3s
  - TTI: ≤2s
- **Monitoring**: Web Vitals measurement (LCP, FID, CLS)

**Overall Status**: ✅ **APPROVED** - All gates pass with justifications

## Project Structure

### Documentation (this feature)

```text
specs/copilot/add-react-web-app-shadcn-ui/
├── plan.md              # This file (/speckit.plan command output)
├── spec.md              # Feature specification
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
└── contracts/           # Phase 1 output (/speckit.plan command)
    ├── users-api.yaml       # Users endpoints OpenAPI spec
    ├── roles-api.yaml       # Roles endpoints OpenAPI spec
    ├── groups-api.yaml      # Groups endpoints OpenAPI spec
    ├── service-accounts-api.yaml
    ├── sessions-api.yaml
    └── providers-api.yaml
```

### Source Code (repository root)

```text
src/
├── OpenIdentityStack.AppHost/          # .NET Aspire orchestration
│   ├── AppHost.cs                 # (Modified) Add admin web app resource
│   └── OpenIdentityStack.AppHost.csproj
├── OpenIdentityStack.Api/              # Existing API (unchanged)
├── OpenIdentityStack.AdminWeb/         # NEW: React admin application
│   ├── public/                    # Static assets
│   ├── src/
│   │   ├── features/              # Feature-based organization
│   │   │   ├── auth/              # Authentication module
│   │   │   │   ├── components/    # Login, callback, etc.
│   │   │   │   ├── hooks/         # useAuth, useRequireAuth
│   │   │   │   └── services/      # OAuth client configuration
│   │   │   ├── users/             # User management
│   │   │   │   ├── components/    # UserList, UserDetail, UserForm
│   │   │   │   ├── hooks/         # useUsers, useUser, useCreateUser
│   │   │   │   └── api/           # API client for users endpoints
│   │   │   ├── roles/             # Role management
│   │   │   ├── groups/            # Group management
│   │   │   ├── service-accounts/  # Service account management
│   │   │   ├── sessions/          # Session management
│   │   │   └── providers/         # Provider management
│   │   ├── components/            # Shared UI components
│   │   │   ├── ui/                # Shadcn UI components
│   │   │   ├── layout/            # AppShell, Sidebar, Header
│   │   │   └── common/            # DataTable, ConfirmDialog, etc.
│   │   ├── lib/                   # Utilities and helpers
│   │   │   ├── api/               # Base API client, error handling
│   │   │   ├── auth/              # Auth utilities, token management
│   │   │   ├── utils.ts           # Common utilities
│   │   │   └── constants.ts       # App constants
│   │   ├── hooks/                 # Global custom hooks
│   │   ├── routes/                # Route definitions
│   │   ├── types/                 # TypeScript type definitions
│   │   ├── App.tsx                # Root component
│   │   ├── main.tsx               # Entry point
│   │   └── vite-env.d.ts          # Vite type declarations
│   ├── tests/                     # Test files
│   │   ├── unit/                  # Vitest unit tests
│   │   ├── integration/           # API integration tests
│   │   └── e2e/                   # Playwright E2E tests
│   ├── .env.example               # Environment variable template
│   ├── .eslintrc.cjs              # ESLint configuration
│   ├── .prettierrc                # Prettier configuration
│   ├── components.json            # Shadcn UI configuration
│   ├── index.html                 # HTML entry point
│   ├── package.json               # Node dependencies
│   ├── postcss.config.js          # PostCSS configuration
│   ├── tailwind.config.js         # Tailwind CSS configuration
│   ├── tsconfig.json              # TypeScript configuration
│   ├── tsconfig.node.json         # TypeScript config for Node scripts
│   ├── vite.config.ts             # Vite configuration
│   └── vitest.config.ts           # Vitest test configuration
└── OpenIdentityStack.ServiceDefaults/  # Existing shared defaults

tests/
├── OpenIdentityStack.AdminWeb.Tests/   # NEW: .NET test project for E2E setup
│   ├── Fixtures/                  # Test fixtures
│   │   └── AdminWebFixture.cs     # Aspire test host with admin web
│   ├── E2E/                       # E2E test orchestration
│   │   └── AdminWebE2ETests.cs    # Verify app starts, health checks
│   └── OpenIdentityStack.AdminWeb.Tests.csproj
└── [existing test projects...]
```

**Structure Decision**: Web application architecture (Option 2) with separation between backend API and frontend admin web app. The React app is organized by feature modules that align with the admin API endpoints, maintaining vertical slice principles within the frontend architecture. The Aspire AppHost orchestrates both the API and the new admin web app.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations requiring justification. All constitution principles are satisfied.

---

## Phase 0: Research (✅ COMPLETE)

**Status**: All technical unknowns resolved  
**Output**: [research.md](research.md)

**Key Decisions Made**:
1. **Build Tool**: Vite 8+ for fast HMR and optimal developer experience
2. **UI Library**: Shadcn UI + Tailwind CSS for accessible, customizable components
3. **Authentication**: oidc-client-ts for OAuth2/OIDC with PKCE support
4. **API State**: TanStack Query v5 for data synchronization and caching
5. **Orchestration**: .NET Aspire AddNpmApp() for integrated development

**Security Approach**:
- Access tokens: In-memory storage (React state)
- OIDC client state: session storage through `oidc-client-ts`; API access tokens are supplied through the auth context and bearer-token interceptor
- PKCE enabled for all auth flows

---

## Phase 1: Design & Contracts (✅ COMPLETE)

**Status**: Data models, API contracts, and developer guide created  
**Outputs**:
- [data-model.md](data-model.md) - TypeScript types and Zod validation schemas
- [contracts/api-summary.md](contracts/api-summary.md) - Complete API endpoint reference
- [quickstart.md](quickstart.md) - Developer setup and usage guide

**Data Model Coverage**:
- ✅ User management (User, UserListItem, UserStatus)
- ✅ Role management (Role, RoleListItem, permissions)
- ✅ Group management (Group, GroupMember, GroupMapping)
- ✅ Service accounts (ServiceAccount, credentials, certificates)
- ✅ Sessions (Session, SessionStatus)
- ✅ Providers (Provider, ProviderType, OIDC/OAuth2/SAML2)
- ✅ Common types (Pagination, ApiError, ApiResult)
- ✅ Validation schemas (Zod for all create/update operations)

**API Contracts**:
- ✅ 45+ endpoints documented across 6 resource groups
- ✅ Permission requirements defined
- ✅ Request/response examples provided
- ✅ Error response patterns documented

**Agent Context**:
- ✅ Updated GitHub Copilot context with TypeScript/React/Node.js stack

---

## Post-Phase 1 Constitution Re-Check

*All gates still PASS after design phase.*

### Updated Assessment:

**I. Test-First Development**: ✅ PASS
- Test strategy defined: Vitest + React Testing Library + Playwright
- TDD workflow documented in quickstart guide

**II. Clean Code Standards**: ✅ PASS
- ESLint + Prettier configuration specified
- TypeScript strict mode enforced
- Component organization follows feature-based structure

**III. Vertical Slice Architecture**: ✅ PASS
- Feature modules organized by admin resource (users, roles, groups, etc.)
- Each feature contains components, hooks, and API client code
- Shared infrastructure in /lib and /components

**IV. Security by Design**: ✅ PASS
- PKCE for OAuth2 flows confirmed
- Token storage strategy: session-scoped OIDC client state with auth-context/API-interceptor access token flow
- Zod validation schemas for all inputs
- Permission-based authorization at UI layer

**V. User Experience Consistency**: ✅ PASS
- Shadcn UI provides design system
- Loading states with 100ms feedback requirement
- Accessible components via Radix UI primitives
- Responsive design with Tailwind breakpoints

**VI. Performance Requirements**: ✅ PASS
- TanStack Query for caching and optimization
- Vite code splitting for route-based lazy loading
- Target metrics defined: <3s page load, <2s TTI

**Overall Status**: ✅ **APPROVED** - Ready for Phase 2 (Implementation Planning)

---

## Next Steps (Phase 2 - Task Generation)

Run `/speckit.tasks` command to generate actionable implementation tasks based on this plan.

**Expected Task Categories**:
1. **Project Setup**: Initialize Vite project, configure Tailwind/Shadcn
2. **Authentication**: Implement OIDC client, auth context, protected routes
3. **API Client**: Create base API client with TanStack Query integration
4. **Feature Modules**: Implement 6 admin modules (users, roles, groups, service accounts, sessions, providers)
5. **UI Components**: Build shared layout, tables, forms, dialogs
6. **Testing**: Write unit tests, integration tests, E2E tests
7. **Aspire Integration**: Configure AppHost.cs, environment variables
8. **Documentation**: Update README, add inline code documentation

---

## Deliverables Summary

### Phase 0 (Research):
- ✅ research.md - Technology decisions and best practices

### Phase 1 (Design):
- ✅ data-model.md - TypeScript types and validation schemas
- ✅ contracts/api-summary.md - API endpoint reference
- ✅ quickstart.md - Developer setup guide
- ✅ Updated agent context (GitHub Copilot)

### Phase 2 (Implementation - Not in this plan):
- 🔄 tasks.md - Generated by `/speckit.tasks` command
- 🔄 Source code implementation
- 🔄 Test suites
- 🔄 Aspire integration

---

## References

- **Feature Spec**: [spec.md](spec.md)
- **Research**: [research.md](research.md)
- **Data Model**: [data-model.md](data-model.md)
- **API Contracts**: [contracts/api-summary.md](contracts/api-summary.md)
- **Quickstart**: [quickstart.md](quickstart.md)
- **Constitution**: [/.specify/memory/constitution.md](/.specify/memory/constitution.md)

---

**Plan Version**: 1.0  
**Created**: 2026-01-18  
**Status**: ✅ Complete (Phases 0-1)  
**Ready for**: Task generation and implementation
