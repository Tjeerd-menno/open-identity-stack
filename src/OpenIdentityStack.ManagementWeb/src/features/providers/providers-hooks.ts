import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createProvider,
  deleteProvider,
  disableProvider,
  enableProvider,
  getProvider,
  getProviders,
  updateProvider,
  type CreateProviderRequest,
  type Provider,
  type UpdateProviderRequest,
} from './providers-api';

export const PROVIDERS_QUERY_KEY = 'providers';

export function useProviders(includeDisabled = true) {
  return useQuery<Provider[]>({
    queryKey: [PROVIDERS_QUERY_KEY, { includeDisabled }],
    queryFn: () => getProviders(includeDisabled),
    staleTime: 30000,
    refetchOnMount: 'always',
  });
}

export function useProvider(providerId: string | undefined) {
  return useQuery<Provider>({
    queryKey: [PROVIDERS_QUERY_KEY, providerId],
    queryFn: () => getProvider(providerId ?? ''),
    enabled: !!providerId,
  });
}

export function useCreateProvider() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateProviderRequest) => createProvider(data),
    onSuccess: (provider) => {
      queryClient.invalidateQueries({ queryKey: [PROVIDERS_QUERY_KEY] });
      queryClient.setQueryData([PROVIDERS_QUERY_KEY, provider.id], provider);
    },
  });
}

export function useUpdateProvider(providerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpdateProviderRequest) => updateProvider(providerId, data),
    onSuccess: (provider) => {
      queryClient.invalidateQueries({ queryKey: [PROVIDERS_QUERY_KEY] });
      queryClient.setQueryData([PROVIDERS_QUERY_KEY, providerId], provider);
    },
  });
}

export function useToggleProvider(providerId: string, nextStatus: 'Active' | 'Disabled') {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => nextStatus === 'Active' ? enableProvider(providerId) : disableProvider(providerId),
    onSuccess: (provider) => {
      queryClient.invalidateQueries({ queryKey: [PROVIDERS_QUERY_KEY] });
      queryClient.setQueryData([PROVIDERS_QUERY_KEY, providerId], provider);
    },
  });
}

export function useDeleteProvider(providerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => deleteProvider(providerId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [PROVIDERS_QUERY_KEY] }),
  });
}
