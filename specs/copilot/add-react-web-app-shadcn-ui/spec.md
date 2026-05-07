# Feature Specification: React Admin Web App with Shadcn UI

**Date**: 2026-01-18 | **Status**: Planning

## Overview

Add a modern React-based web application with Shadcn UI component library to provide a comprehensive administrative interface for the OpenIdentityStack. The application will authenticate against the existing OpenIddict server and consume the existing admin API endpoints.

## Objectives

1. **Administrative Interface**: Provide a modern, user-friendly web UI for managing IAM resources
2. **OAuth2/OIDC Integration**: Authenticate using the existing OpenIddict server with proper OAuth2/OIDC client flow
3. **API Integration**: Consume all existing admin API endpoints for CRUD operations
4. **Aspire Integration**: Integrate the React app into the existing .NET Aspire AppHost for seamless orchestration
5. **Production Ready**: Deliver a production-ready application with proper error handling, loading states, and UX patterns

## User Scenarios

### As an Administrator, I want to:

1. **Authentication**
   - Log in using OAuth2/OIDC flow against the OpenIddict server
   - See my authenticated user information
   - Log out securely

2. **User Management** (via `/api/admin/users`)
   - List all users with pagination and filtering
   - Create new users with username, email, and password
   - View user details including roles and groups
   - Update user information
   - Enable/disable user accounts
   - Delete users
   - Assign/unassign roles to users
   - Link/unlink upstream identity providers

3. **Role Management** (via `/api/admin/roles`)
   - List all roles
   - Create new roles with permissions
   - View role details
   - Update role permissions
   - Delete roles

4. **Group Management** (via `/api/admin/groups`)
   - List all groups
   - Create new groups
   - View group details including members
   - Update group information
   - Delete groups
   - Manage group membership

5. **Service Account Management** (via `/api/admin/service-accounts`)
   - List all service accounts
   - Create new service accounts
   - View service account details
   - Update service account information
   - Delete service accounts

6. **Session Management** (via `/api/admin/sessions`)
   - List active user sessions
   - View session details
   - Revoke individual sessions
   - Revoke all sessions for a user

7. **Provider Management** (via `/api/admin/providers`)
   - List configured identity providers
   - Create new provider configurations
   - View provider details
   - Update provider settings
   - Enable/disable providers
   - Delete providers

## Functional Requirements

### FR1: Authentication & Authorization
- **FR1.1**: Application MUST implement OAuth2/OIDC Authorization Code Flow with PKCE
- **FR1.2**: Application MUST securely store and refresh access tokens
- **FR1.3**: Application MUST handle token expiration and automatic refresh
- **FR1.4**: Application MUST redirect unauthenticated users to login
- **FR1.5**: Application MUST display appropriate error messages for authentication failures

### FR2: User Interface
- **FR2.1**: Application MUST use Shadcn UI components for consistent design
- **FR2.2**: Application MUST be responsive (mobile, tablet, desktop)
- **FR2.3**: Application MUST provide loading indicators for async operations
- **FR2.4**: Application MUST display user-friendly error messages
- **FR2.5**: Application MUST provide navigation between different admin sections
- **FR2.6**: Application MUST implement proper form validation

### FR3: API Integration
- **FR3.1**: Application MUST call existing admin API endpoints with proper authentication
- **FR3.2**: Application MUST handle API errors gracefully
- **FR3.3**: Application MUST implement pagination for list endpoints
- **FR3.4**: Application MUST implement filtering and sorting where applicable

### FR4: Data Management
- **FR4.1**: Application MUST support CRUD operations for all admin resources
- **FR4.2**: Application MUST confirm destructive operations (delete, disable)
- **FR4.3**: Application MUST validate input before submission
- **FR4.4**: Application MUST refresh data after mutations

### FR5: Aspire Integration
- **FR5.1**: Application MUST be orchestrated by .NET Aspire AppHost
- **FR5.2**: Application MUST support hot-reload during development
- **FR5.3**: Application MUST be accessible via Aspire dashboard
- **FR5.4**: Application MUST support environment-based configuration

## Non-Functional Requirements

### NFR1: Performance
- **NFR1.1**: Initial page load MUST complete within 3 seconds
- **NFR1.2**: UI interactions MUST provide feedback within 100ms
- **NFR1.3**: API calls MUST display loading state within 100ms

### NFR2: Security
- **NFR2.1**: Application MUST NOT store sensitive data in localStorage (use secure token storage)
- **NFR2.2**: Application MUST validate all user input client-side
- **NFR2.3**: Application MUST use HTTPS in production
- **NFR2.4**: Application MUST implement CSRF protection

### NFR3: Usability
- **NFR3.1**: Application MUST meet WCAG 2.1 AA accessibility standards
- **NFR3.2**: Application MUST provide clear navigation structure
- **NFR3.3**: Error messages MUST be actionable and user-friendly

### NFR4: Maintainability
- **NFR4.1**: Code MUST follow React best practices (hooks, functional components)
- **NFR4.2**: Components MUST be modular and reusable
- **NFR4.3**: Application MUST have proper TypeScript types
- **NFR4.4**: Application MUST include unit tests for critical components

## Technical Constraints

### TC1: Technology Stack
- **React 18+** with TypeScript
- **Vite** as build tool
- **Shadcn UI** component library
- **TanStack Query** (React Query) for API state management
- **React Router** for client-side routing
- **OAuth2/OIDC client library** for authentication

### TC2: Integration
- Must integrate with existing .NET Aspire 13.1.0 AppHost
- Must work with existing OpenIddict 7.2.0 server
- Must consume existing admin API endpoints without modification

### TC3: Development
- Must support hot-reload during development
- Must be buildable for production deployment
- Must use npm/yarn for package management

## Out of Scope

- Modifying existing admin API endpoints
- Adding new admin API endpoints
- Implementing real-time updates (WebSocket/SignalR)
- Multi-language/internationalization (i18n)
- Dark mode support (can be added later)
- Advanced analytics/reporting
- Mobile native apps

## Success Criteria

1. ✅ Administrator can authenticate via OAuth2/OIDC
2. ✅ Administrator can perform all CRUD operations on users
3. ✅ Administrator can perform all CRUD operations on roles
4. ✅ Administrator can perform all CRUD operations on groups
5. ✅ Administrator can perform all CRUD operations on service accounts
6. ✅ Administrator can view and manage sessions
7. ✅ Administrator can view and manage identity providers
8. ✅ Application is integrated into Aspire AppHost
9. ✅ Application passes all unit and integration tests
10. ✅ Application meets performance and accessibility requirements

## Dependencies

- Existing admin API endpoints must be functional
- OpenIddict server must support OAuth2 client registration
- .NET Aspire AppHost must support Node.js/Vite projects

## Risks & Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| OAuth2/OIDC configuration complexity | High | Medium | Use well-tested library, follow OpenIddict documentation |
| Aspire integration challenges | Medium | Low | Leverage Aspire's Node.js resource support |
| API breaking changes | High | Low | Version API endpoints, use contract tests |
| Performance issues with large datasets | Medium | Medium | Implement proper pagination, virtual scrolling |

## Timeline Estimate

- **Phase 0 (Research)**: 1 day - Research React + Vite + Shadcn + OAuth2 best practices
- **Phase 1 (Design)**: 1 day - Data models, contracts, quickstart guide
- **Phase 2 (Implementation)**: 5-7 days - Build application with tests
- **Total**: 7-9 days

## References

- OpenIddict Documentation: https://documentation.openiddict.com/
- Shadcn UI: https://ui.shadcn.com/
- .NET Aspire: https://learn.microsoft.com/en-us/dotnet/aspire/
- OAuth2/OIDC: https://oauth.net/2/
