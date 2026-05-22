# React Admin Web App Implementation Plan

**Feature**: Add React-based admin web application with Shadcn UI  
**Branch**: `copilot/add-react-web-app-shadcn-ui`  
**Status**: Implemented and code-aligned  
**Date**: 2026-01-18

---

## Overview

This directory captures the implemented React-based administrative web application for OpenIdentityStack. The app provides a comprehensive UI for managing users, roles, groups, service accounts, service permission registrations, clients, sessions, settings, and identity providers.

---

## Documentation Structure

```
specs/copilot/add-react-web-app-shadcn-ui/
├── README.md             # This file - overview and navigation
├── spec.md               # Feature specification (requirements, user scenarios)
├── plan.md               # Implementation plan (phases 0-1 complete)
├── research.md           # Phase 0: Technology research and decisions
├── data-model.md         # Phase 1: TypeScript types and data models
├── quickstart.md         # Phase 1: Developer setup guide
└── contracts/            # Phase 1: API endpoint specifications
    └── api-summary.md    # Complete API reference (45+ endpoints)
```

---

## Quick Navigation

### 📋 Requirements & Planning
- **[spec.md](spec.md)** - Start here to understand the feature requirements
- **[plan.md](plan.md)** - Complete implementation plan with all phases

### 🔬 Phase 0: Research (✅ Complete)
- **[research.md](research.md)** - Technology decisions and best practices
  - React + Vite setup
  - Shadcn UI integration
  - OAuth2/OIDC authentication strategy
  - TanStack Query patterns
  - .NET Aspire integration

### 🏗️ Phase 1: Design (✅ Complete)
- **[data-model.md](data-model.md)** - TypeScript types and Zod validation schemas
- **[contracts/api-summary.md](contracts/api-summary.md)** - Complete API endpoint reference
- **[quickstart.md](quickstart.md)** - Developer setup and usage guide

### 🚀 Next Steps (Phase 2)
Run the `/speckit.tasks` command to generate implementation tasks based on this plan.

---

## Technology Stack

### Frontend
- **Framework**: React 19+ with TypeScript 6+
- **Build Tool**: Vite 8+
- **UI Library**: Shadcn UI (Radix UI + Tailwind CSS)
- **State Management**: TanStack Query v5
- **Routing**: React Router v7
- **Authentication**: oidc-client-ts (OAuth2/OIDC with PKCE)
- **Validation**: Zod
- **Testing**: Vitest, React Testing Library, Playwright

### Backend Integration
- **API**: Existing OpenIdentityStack.Api (no changes required)
- **Auth Server**: OpenIddict 7.5.0
- **Orchestration**: .NET Aspire 13.3.x AppHost

---

## Key Features

### Admin Modules
1. **Users** - Create, list, update, disable, delete users
2. **Roles** - Manage roles and permissions
3. **Groups** - Organize users into groups with role mappings
4. **Service Accounts** - Manage OAuth2 clients for machine-to-machine auth
5. **Service Permission Registry** - Register service-owned permissions, lifecycle, ownership, and maintainers
6. **Clients** - Manage OAuth2/OIDC application clients
7. **Sessions** - View and revoke active user sessions
8. **Settings** - Configure authentication defaults and fallback behavior
9. **Providers** - Configure external identity providers

### Authentication Flow
- OAuth2 Authorization Code Flow with PKCE
- OIDC client state uses session storage; access tokens are still provided through the auth context and API interceptor
- Automatic token refresh
- Protected routes with permission checks

### User Experience
- Responsive design (mobile, tablet, desktop)
- Loading states with <100ms feedback
- WCAG 2.1 AA accessibility compliance
- User-friendly error messages

---

## Design Decisions

### ✅ Security
- **PKCE**: Always enabled for OAuth2 flows (required for SPAs)
- **Token Storage**: OIDC client state in session storage; access tokens flow through the auth context and API interceptor
- **Input Validation**: Client-side Zod validation + server-side validation
- **CORS**: Whitelisted origins only

### ✅ Performance
- **Code Splitting**: Route-based lazy loading
- **Caching**: TanStack Query for request deduplication and background refetching
- **Bundle Optimization**: Vite tree-shaking and minification
- **Target Metrics**: <3s page load, <2s time to interactive

### ✅ Developer Experience
- **Hot Module Replacement**: Instant feedback during development
- **Type Safety**: Full TypeScript coverage with strict mode
- **Testing**: TDD workflow with Vitest + Playwright
- **Aspire Integration**: Unified development environment

---

## Project Structure (Planned)

```
src/OpenIdentityStack.AdminWeb/
├── src/
│   ├── features/              # Feature modules
│   │   ├── auth/              # Authentication
│   │   ├── users/             # User management
│   │   ├── roles/             # Role management
│   │   ├── groups/            # Group management
│   │   ├── service-accounts/  # Service account management
│   │   ├── sessions/          # Session management
│   │   └── providers/         # Provider management
│   ├── components/            # Shared UI components
│   │   ├── ui/                # Shadcn UI components
│   │   ├── layout/            # Layout components
│   │   └── common/            # Reusable components
│   ├── lib/                   # Utilities
│   │   ├── api/               # API client
│   │   ├── auth/              # Auth utilities
│   │   └── utils.ts           # Helpers
│   ├── hooks/                 # Custom hooks
│   ├── routes/                # Route definitions
│   └── types/                 # TypeScript types
├── tests/
│   ├── unit/                  # Vitest tests
│   └── e2e/                   # Playwright tests
├── package.json
├── vite.config.ts
├── tsconfig.json
└── tailwind.config.js
```

---

## API Integration

The React app consumes **45+ API endpoints** across 6 resource groups:

| Resource Group | Base URL | Endpoints |
|----------------|----------|-----------|
| Users | `/api/admin/users` | 15 endpoints |
| Roles | `/api/admin/roles` | 6 endpoints |
| Groups | `/api/admin/groups` | 10 endpoints |
| Service Accounts | `/api/admin/service-accounts` | 8 endpoints |
| Sessions | `/api/admin/sessions` | 4 endpoints |
| Providers | `/api/admin/providers` | 6 endpoints |

All endpoints require:
- **Authentication**: Bearer token (JWT) from OpenIddict
- **Authorization**: Specific permissions (e.g., `users:read`, `roles:create`)

See [contracts/api-summary.md](contracts/api-summary.md) for complete API reference.

---

## Constitution Compliance

All core principles from the project constitution are satisfied:

✅ **I. Test-First Development** - TDD workflow with Vitest + Playwright  
✅ **II. Clean Code Standards** - ESLint + Prettier + TypeScript strict mode  
✅ **III. Vertical Slice Architecture** - Feature-based organization  
✅ **IV. Security by Design** - PKCE, secure token storage, input validation  
✅ **V. User Experience Consistency** - Shadcn UI design system, WCAG AA compliance  
✅ **VI. Performance Requirements** - Code splitting, caching, <3s page load

---

## Timeline Estimate

- **Phase 0 (Research)**: ✅ Complete - 1 day
- **Phase 1 (Design)**: ✅ Complete - 1 day
- **Phase 2 (Task Generation)**: 🔄 Pending - Use `/speckit.tasks` command
- **Phase 3 (Implementation)**: 🔄 Pending - 5-7 days
- **Total**: 7-9 days

---

## Getting Started

### For Reviewers
1. Read [spec.md](spec.md) to understand requirements
2. Review [plan.md](plan.md) for technical approach
3. Check [research.md](research.md) for technology decisions

### For Developers
1. Read [quickstart.md](quickstart.md) for setup instructions
2. Review [data-model.md](data-model.md) for TypeScript types
3. Reference [contracts/api-summary.md](contracts/api-summary.md) for API endpoints

### For Implementers
1. Generate tasks: Run `/speckit.tasks` command
2. Follow TDD workflow: Write tests first
3. Implement features based on generated tasks

---

## Status

- ✅ **Phase 0 (Research)**: Complete - All technology decisions made
- ✅ **Phase 1 (Design)**: Complete - Data models, contracts, quickstart guide created
- ✅ **Agent Context**: Updated GitHub Copilot instructions
- 🔄 **Phase 2 (Tasks)**: Ready to generate implementation tasks
- 🔄 **Phase 3 (Implementation)**: Awaiting task generation

---

## References

- **OpenIddict Documentation**: https://documentation.openiddict.com/
- **Shadcn UI**: https://ui.shadcn.com/
- **TanStack Query**: https://tanstack.com/query/latest
- **Vite**: https://vitejs.dev/
- **.NET Aspire**: https://learn.microsoft.com/en-us/dotnet/aspire/

---

**Last Updated**: 2026-01-18  
**Version**: 1.0  
**Status**: Implemented; keep this spec aligned to the codebase when dependencies or admin modules change.
