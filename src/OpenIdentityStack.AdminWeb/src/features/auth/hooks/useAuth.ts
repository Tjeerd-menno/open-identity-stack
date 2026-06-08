/**
 * T052: Implement useAuth custom hook
 * 
 * Custom hook for accessing authentication state and methods.
 */

import { useContext } from 'react';
import {
  hasAnyPermission,
  hasEveryPermission,
  hasPermission,
} from '@openidentitystack/admin-api-client';
import { AuthContext, type AuthContextValue } from '../auth-context';

/**
 * Hook to access authentication context
 * 
 * Provides access to:
 * - Authentication state (isAuthenticated, isLoading, user)
 * - Authentication methods (login, logout)
 * - Access token retrieval
 * 
 * @throws {Error} If used outside of AuthProvider
 * 
 * @example
 * ```tsx
 * const { isAuthenticated, user, login, logout } = useAuth();
 * 
 * if (!isAuthenticated) {
 *   return <button onClick={login}>Sign In</button>;
 * }
 * 
 * return (
 *   <div>
 *     <p>Welcome, {user?.name}!</p>
 *     <button onClick={logout}>Sign Out</button>
 *   </div>
 * );
 * ```
 */
export const useAuth = (): AuthContextValue => {
  const context = useContext(AuthContext);
  
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  
  return context;
};

/**
 * Hook to check if user has a specific permission
 * 
 * @param permission - Permission string to check (e.g., "users:create")
 * @returns True if user has the permission, false otherwise
 * 
 * @example
 * ```tsx
 * const canCreateUsers = usePermission('users:create');
 * 
 * if (canCreateUsers) {
 *   return <button onClick={createUser}>Create User</button>;
 * }
 * ```
 */
export const usePermission = (permission: string): boolean => {
  const { user, isAuthenticated } = useAuth();
  
  if (!isAuthenticated || !user) {
    return false;
  }
  
  return hasPermission(user.permissions, permission);
};

/**
 * Hook to check if user has any of the specified permissions
 * 
 * @param permissions - Array of permission strings
 * @returns True if user has at least one of the permissions, false otherwise
 * 
 * @example
 * ```tsx
 * const canManageUsers = usePermissions(['users:create', 'users:update', 'users:delete']);
 * ```
 */
export const usePermissions = (permissions: string[]): boolean => {
  const { user, isAuthenticated } = useAuth();
  
  if (!isAuthenticated || !user) {
    return false;
  }
  
  return hasAnyPermission(user.permissions, permissions);
};

/**
 * Hook to check if user has all of the specified permissions
 * 
 * @param permissions - Array of permission strings
 * @returns True if user has all permissions, false otherwise
 * 
 * @example
 * ```tsx
 * const isAdmin = useAllPermissions(['users:*', 'roles:*', 'groups:*']);
 * ```
 */
export const useAllPermissions = (permissions: string[]): boolean => {
  const { user, isAuthenticated } = useAuth();
  
  if (!isAuthenticated || !user) {
    return false;
  }
  
  return hasEveryPermission(user.permissions, permissions);
};
