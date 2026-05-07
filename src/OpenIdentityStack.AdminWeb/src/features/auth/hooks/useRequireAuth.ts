/**
 * T053: Implement useRequireAuth hook for protected routes
 * 
 * Hook that ensures the user is authenticated before rendering protected content.
 * Redirects to login page if user is not authenticated.
 */

import { useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from './useAuth';

/**
 * Hook to require authentication for a component
 * 
 * Automatically redirects to login page if user is not authenticated.
 * After successful login, user is redirected back to the original page.
 * 
 * @param requiredPermissions - Optional array of permissions required to access the route
 * @returns Authentication state with loading and error information
 * 
 * @example
 * ```tsx
 * const UserManagementPage = () => {
 *   const { isLoading } = useRequireAuth(['users:read']);
 *   
 *   if (isLoading) {
 *     return <LoadingSpinner />;
 *   }
 *   
 *   return <UserList />;
 * };
 * ```
 */
export const useRequireAuth = (requiredPermissions?: string[]) => {
  const { isAuthenticated, isLoading, user, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    // Wait for auth state to be determined
    if (isLoading) {
      return;
    }

    // Redirect to login if not authenticated
    if (!isAuthenticated) {
      // The OIDC library will handle storing and restoring the return URL
      login();
      return;
    }

    // Check required permissions if specified
    if (requiredPermissions && requiredPermissions.length > 0) {
      const hasRequiredPermissions = requiredPermissions.every(permission =>
        user?.permissions.includes(permission)
      );

      if (!hasRequiredPermissions) {
        // User is authenticated but doesn't have required permissions
        // Redirect to access denied page
        navigate('/access-denied', {
          state: { from: location },
          replace: true,
        });
      }
    }
  }, [isAuthenticated, isLoading, user, requiredPermissions, navigate, location, login]);

  return {
    isLoading,
    isAuthenticated,
    user,
  };
};

/**
 * Hook to check if user is authorized for a specific action
 * 
 * Similar to useRequireAuth but does not redirect. Returns authorization state instead.
 * Useful for conditional rendering without navigation.
 * 
 * @param requiredPermissions - Array of permissions required
 * @returns Object with authorization state
 * 
 * @example
 * ```tsx
 * const { isAuthorized, isLoading } = useAuthorization(['users:delete']);
 * 
 * if (isLoading) return <LoadingSpinner />;
 * if (!isAuthorized) return null;
 * 
 * return <DeleteUserButton />;
 * ```
 */
export const useAuthorization = (requiredPermissions: string[]) => {
  const { isAuthenticated, isLoading, user } = useAuth();

  if (isLoading) {
    return { isAuthorized: false, isLoading: true };
  }

  if (!isAuthenticated || !user) {
    return { isAuthorized: false, isLoading: false };
  }

  const isAuthorized = requiredPermissions.every(permission =>
    user.permissions.includes(permission)
  );

  return { isAuthorized, isLoading: false };
};
