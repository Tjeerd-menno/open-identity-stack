/**
 * T058: Add authentication routes to router
 * T096: Add user management routes to router
 * T115: Add role management routes to router
 * 
 * Route configuration with authentication and protected routes.
 */

import { createBrowserRouter } from 'react-router-dom';
import { AppShell } from '@/components/layout/AppShell';
import { ProtectedRoute, AccessDenied } from '@/features/auth/components/ProtectedRoute';
import { Login } from '@/features/auth/components/Login';
import { Callback } from '@/features/auth/components/Callback';
import { SilentCallback } from '@/features/auth/components/SilentCallback';

// User Management
import { UserList, UserDetail, CreateUserPage, EditUserPage } from '@/features/users';

// Role Management
import { RolesPage } from '@/features/roles/pages/RolesPage';
import { CreateRolePage } from '@/features/roles/pages/CreateRolePage';
import { RoleDetailPage } from '@/features/roles/pages/RoleDetailPage';

// Group Management
import { GroupsPage, GroupDetailPage, CreateGroupPage, EditGroupPage } from '@/features/groups';

// Service Account Management
import { ServiceAccountsPage, ServiceAccountDetailPage, CreateServiceAccountPage, EditServiceAccountPage } from '@/features/service-accounts';

// Permissions
import { ApplicationPermissionsPage, RegisterApplicationPage, RegisteredApplicationDetailPage } from '@/features/application-permissions';

// Session Management
import { SessionsPage, SessionDetailPage } from '@/features/sessions';

// Provider Management
import { ProvidersPage, CreateProviderPage, ProviderDetailPage, EditProviderPage } from '@/features/providers';

// Settings
import { SettingsPage } from '@/features/settings';

// Client Management
import { ClientsPage, CreateClientPage, ClientDetailPage, EditClientPage } from '@/features/clients';

// Dashboard
import { DashboardPage } from '@/features/dashboard';

// Error pages
import { NotFound } from '@/components/common/NotFound';

export const router = createBrowserRouter([
  // Public authentication routes
  {
    path: '/login',
    element: <Login />,
  },
  {
    path: '/auth/callback',
    element: <Callback />,
  },
  {
    path: '/auth/silent-callback',
    element: <SilentCallback />,
  },
  {
    path: '/access-denied',
    element: <AccessDenied />,
  },
  
  // Protected application routes
  {
    path: '/',
    element: (
      <ProtectedRoute requireAnyPermission>
        <AppShell />
      </ProtectedRoute>
    ),
    children: [
      { index: true, element: <DashboardPage /> },
      
      // User Management Routes
      { 
        path: 'users', 
        element: <UserList /> 
      },
      { 
        path: 'users/create', 
        element: <CreateUserPage /> 
      },
      { 
        path: 'users/:userId', 
        element: <UserDetail /> 
      },
      { 
        path: 'users/:userId/edit', 
        element: <EditUserPage /> 
      },
      
      // Role Management Routes
      { 
        path: 'roles', 
        element: <RolesPage /> 
      },
      { 
        path: 'roles/new', 
        element: <CreateRolePage /> 
      },
      { 
        path: 'roles/:id', 
        element: <RoleDetailPage /> 
      },
      
      // Group Management Routes
      { 
        path: 'groups', 
        element: <GroupsPage /> 
      },
      { 
        path: 'groups/new', 
        element: <CreateGroupPage /> 
      },
      { 
        path: 'groups/:id', 
        element: <GroupDetailPage /> 
      },
      { 
        path: 'groups/:id/edit', 
        element: <EditGroupPage /> 
      },
      
      // Service Account Management Routes
      { 
        path: 'service-accounts', 
        element: <ServiceAccountsPage /> 
      },
      { 
        path: 'service-accounts/new', 
        element: <CreateServiceAccountPage /> 
      },
      { 
        path: 'service-accounts/:id', 
        element: <ServiceAccountDetailPage /> 
      },
      { 
        path: 'service-accounts/:id/edit', 
        element: <EditServiceAccountPage /> 
      },

      // Permissions Routes
      {
        path: 'application-permissions',
        element: <ApplicationPermissionsPage />
      },
      {
        path: 'application-permissions/new',
        element: <RegisterApplicationPage />
      },
      {
        path: 'application-permissions/:id',
        element: <RegisteredApplicationDetailPage />
      },
      
      // Session Management Routes
      { 
        path: 'sessions', 
        element: <SessionsPage /> 
      },
      { 
        path: 'sessions/:id', 
        element: <SessionDetailPage /> 
      },
      
      // Provider Management Routes
      { 
        path: 'providers', 
        element: <ProvidersPage /> 
      },
      { 
        path: 'providers/new', 
        element: <CreateProviderPage /> 
      },
      { 
        path: 'providers/:id', 
        element: <ProviderDetailPage /> 
      },
      { 
        path: 'providers/:id/edit', 
        element: <EditProviderPage /> 
      },
      
      // Client Management Routes
      { 
        path: 'clients', 
        element: <ClientsPage /> 
      },
      { 
        path: 'clients/new', 
        element: <CreateClientPage /> 
      },
      { 
        path: 'clients/:id', 
        element: <ClientDetailPage /> 
      },
      { 
        path: 'clients/:id/edit', 
        element: <EditClientPage /> 
      },
      
      // Settings Route
      { 
        path: 'settings', 
        element: <SettingsPage /> 
      },
      
      // 404 catch-all
      { 
        path: '*', 
        element: <NotFound /> 
      },
    ],
  },
  
  // Global 404 for non-authenticated routes
  {
    path: '*',
    element: <NotFound />,
  },
]);
