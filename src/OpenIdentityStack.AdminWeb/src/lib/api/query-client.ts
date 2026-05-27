import { QueryClient } from '@tanstack/react-query';
import type { ApiError } from '@/types';

type QueryKeyParams = Record<string, unknown> | undefined;

const getErrorStatus = (error: unknown): number | undefined => {
  if (typeof error !== 'object' || error === null || !('status' in error)) {
    return undefined;
  }

  const status = (error as Partial<ApiError>).status;
  return typeof status === 'number' ? status : undefined;
};

/**
 * Configure TanStack Query client
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000, // 5 minutes
      gcTime: 10 * 60 * 1000, // 10 minutes (formerly cacheTime)
      retry: (failureCount, error) => {
        const status = getErrorStatus(error);

        // Don't retry on auth errors (401, 403)
        if (status === 401 || status === 403) {
          return false;
        }
        // Don't retry on client errors (4xx)
        if (status !== undefined && status >= 400 && status < 500) {
          return false;
        }
        // Retry up to 2 times for network errors and 5xx
        return failureCount < 2;
      },
      refetchOnWindowFocus: false, // Reduce noise in admin apps
      refetchOnMount: true,
      refetchOnReconnect: true,
    },
    mutations: {
      retry: false, // Don't auto-retry mutations
    },
  },
});

/**
 * Query key factory for consistent key generation
 */
export const queryKeys = {
  // Users
  users: {
    all: ['users'] as const,
    lists: () => [...queryKeys.users.all, 'list'] as const,
    list: (params?: QueryKeyParams) => [...queryKeys.users.lists(), params] as const,
    details: () => [...queryKeys.users.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.users.details(), id] as const,
    roles: (id: string) => [...queryKeys.users.detail(id), 'roles'] as const,
    groups: (id: string) => [...queryKeys.users.detail(id), 'groups'] as const,
    upstreamIdentities: (id: string) => [...queryKeys.users.detail(id), 'upstream-identities'] as const,
  },

  // Roles
  roles: {
    all: ['roles'] as const,
    lists: () => [...queryKeys.roles.all, 'list'] as const,
    list: (params?: QueryKeyParams) => [...queryKeys.roles.lists(), params] as const,
    details: () => [...queryKeys.roles.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.roles.details(), id] as const,
  },

  // Groups
  groups: {
    all: ['groups'] as const,
    lists: () => [...queryKeys.groups.all, 'list'] as const,
    list: (params?: QueryKeyParams) => [...queryKeys.groups.lists(), params] as const,
    details: () => [...queryKeys.groups.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.groups.details(), id] as const,
    members: (id: string) => [...queryKeys.groups.detail(id), 'members'] as const,
    mappings: (id: string) => [...queryKeys.groups.detail(id), 'mappings'] as const,
  },

  // Service Accounts
  serviceAccounts: {
    all: ['service-accounts'] as const,
    lists: () => [...queryKeys.serviceAccounts.all, 'list'] as const,
    list: (params?: QueryKeyParams) => [...queryKeys.serviceAccounts.lists(), params] as const,
    details: () => [...queryKeys.serviceAccounts.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.serviceAccounts.details(), id] as const,
  },

  // Sessions
  sessions: {
    all: ['sessions'] as const,
    lists: () => [...queryKeys.sessions.all, 'list'] as const,
    list: (params?: QueryKeyParams) => [...queryKeys.sessions.lists(), params] as const,
    details: () => [...queryKeys.sessions.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.sessions.details(), id] as const,
  },

  // Providers
  providers: {
    all: ['providers'] as const,
    lists: () => [...queryKeys.providers.all, 'list'] as const,
    list: (params?: QueryKeyParams) => [...queryKeys.providers.lists(), params] as const,
    details: () => [...queryKeys.providers.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.providers.details(), id] as const,
  },
};
