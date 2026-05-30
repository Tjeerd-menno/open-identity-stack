/**
 * T057: Create ProtectedRoute wrapper component
 * 
 * Route wrapper that ensures user is authenticated and authorized
 * before rendering protected content.
 */

import React, { useCallback } from 'react';
import { Navigate, useLocation, type Location } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';

interface ProtectedRouteState {
  from?: Location;
}

function getFromPathname(state: unknown): string | undefined {
  if (typeof state !== 'object' || state === null || !('from' in state)) {
    return undefined;
  }

  const { from } = state as ProtectedRouteState;
  return from?.pathname;
}

/**
 * Props for ProtectedRoute component
 */
export interface ProtectedRouteProps {
  children: React.ReactNode;
  requiredPermissions?: string[];
  requireAnyPermission?: boolean;
}

/**
 * Protected route wrapper component
 * 
 * Wraps routes that require authentication and optionally specific permissions.
 * 
 * Behavior:
 * - If loading: Shows loading spinner
 * - If not authenticated: Redirects to login page
 * - If authenticated but missing permissions: Shows access denied message
 * - If authenticated with permissions: Renders children
 * 
 * @example
 * ```tsx
 * <Route path="/users" element={
 *   <ProtectedRoute requiredPermissions={['users:read']}>
 *     <UserList />
 *   </ProtectedRoute>
 * } />
 * ```
 */
export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({
  children,
  requiredPermissions = [],
  requireAnyPermission = false,
}) => {
  const { isAuthenticated, isLoading, isLoggingOut, user } = useAuth();
  const location = useLocation();

  // Debug logging
  console.log('[ProtectedRoute] isLoading:', isLoading, 'isAuthenticated:', isAuthenticated, 'isLoggingOut:', isLoggingOut, 'path:', location.pathname);

  // Show loading state while checking authentication
  if (isLoading) {
    console.log('[ProtectedRoute] Showing loading spinner');
    return (
      <div className="flex min-h-screen items-center justify-center">
        <LoadingSpinner size="lg" />
      </div>
    );
  }

  // If logging out, show loading spinner to prevent redirect to /login
  // The logout flow will redirect to the OIDC logout endpoint
  if (isLoggingOut) {
    console.log('[ProtectedRoute] Logging out, showing spinner');
    return (
      <div className="flex min-h-screen items-center justify-center">
        <LoadingSpinner size="lg" />
      </div>
    );
  }

  // Redirect to login if not authenticated
  if (!isAuthenticated) {
    console.log('[ProtectedRoute] Not authenticated, redirecting to /login');
    // Save the current location to redirect back after login
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  console.log('[ProtectedRoute] Authenticated, rendering children');

  // Require at least one permission (admin access gate)
  if (requireAnyPermission && (!user?.permissions || user.permissions.length === 0)) {
    return <Navigate to="/access-denied" state={{ from: location }} replace />;
  }

  // Check required permissions if specified
  if (requiredPermissions.length > 0) {
    const hasRequiredPermissions = requiredPermissions.every(permission =>
      user?.permissions.includes(permission)
    );

    if (!hasRequiredPermissions) {
      // User is authenticated but doesn't have required permissions
      return <Navigate to="/access-denied" state={{ from: location }} replace />;
    }
  }

  // User is authenticated and authorized
  return <>{children}</>;
};

/**
 * Access Denied page component
 * 
 * Shown when user is authenticated but lacks required permissions
 */
export const AccessDenied: React.FC = () => {
  const location = useLocation();
  const { logout } = useAuth();
  const from = getFromPathname(location.state);

  const handleSignOut = useCallback(async () => {
    console.log('[AccessDenied] Sign out button clicked');
    try {
      await logout();
      // signoutRedirect() will handle the redirect - don't navigate manually
      console.log('[AccessDenied] Logout completed (this may not be reached if redirect happened)');
    } catch (error) {
      console.error('[AccessDenied] Sign out error:', error);
    }
  }, [logout]);

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50">
      <div className="text-center space-y-4">
        <div className="flex justify-center">
          <div className="h-24 w-24 rounded-full bg-red-100 flex items-center justify-center">
            <svg
              className="h-12 w-12 text-red-600"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
          </div>
        </div>
        <h1 className="text-3xl font-bold text-gray-900">Access Denied</h1>
        <p className="text-gray-600 max-w-md">
          You don't have permission to access this page.
          {from && (
            <span className="block mt-2 text-sm">
              Attempted to access: <code className="bg-gray-100 px-2 py-1 rounded">{from}</code>
            </span>
          )}
        </p>
        <p className="text-sm text-gray-500">
          If you believe you should have access, please contact your administrator.
        </p>
        <div className="pt-4 flex items-center justify-center gap-3">
          <button
            type="button"
            onClick={handleSignOut}
            className="inline-flex px-6 py-3 bg-gray-200 text-gray-900 rounded-md hover:bg-gray-300 transition-colors"
          >
            Sign out
          </button>
          <a
            href="/"
            className="inline-block px-6 py-3 bg-primary text-primary-foreground rounded-md hover:bg-primary/90 transition-colors"
          >
            Back to Home
          </a>
        </div>
      </div>
    </div>
  );
};
