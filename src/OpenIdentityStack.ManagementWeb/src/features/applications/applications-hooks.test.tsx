import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApplicationClientType, ApplicationProfile } from './applications-api';
import {
  APPLICATIONS_QUERY_KEY,
  useAddApplicationSecret,
  useApplication,
  useApplicationCredentials,
  useApplicationProfilePolicies,
  useApplications,
  useCreateApplication,
  useDeleteApplication,
  useDisableApplication,
  useEnableApplication,
  useRevokeApplicationCredential,
  useUpdateApplication,
} from './applications-hooks';

const api = vi.hoisted(() => ({
  addApplicationSecret: vi.fn(),
  getApplication: vi.fn(),
  getApplicationCredentials: vi.fn(),
  getApplicationProfilePolicies: vi.fn(),
  getApplications: vi.fn(),
  createApplication: vi.fn(),
  deleteApplication: vi.fn(),
  disableApplication: vi.fn(),
  enableApplication: vi.fn(),
  revokeApplicationCredential: vi.fn(),
  updateApplicationMetadata: vi.fn(),
}));

vi.mock('./applications-api', async (importOriginal) => ({
  ...(await importOriginal<typeof import('./applications-api')>()),
  addApplicationSecret: api.addApplicationSecret,
  getApplication: api.getApplication,
  getApplicationProfilePolicies: api.getApplicationProfilePolicies,
  getApplications: api.getApplications,
  listApplicationCredentials: api.getApplicationCredentials,
  createApplication: api.createApplication,
  deleteApplication: api.deleteApplication,
  disableApplication: api.disableApplication,
  enableApplication: api.enableApplication,
  revokeApplicationCredential: api.revokeApplicationCredential,
  updateApplicationMetadata: api.updateApplicationMetadata,
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  return {
    queryClient,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    ),
  };
}

describe('applications hooks', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('reads applications, application details, credentials, and profile policies', async () => {
    api.getApplications.mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
    api.getApplication.mockResolvedValueOnce({ id: 'app-1' });
    api.getApplicationCredentials.mockResolvedValueOnce([]);
    api.getApplicationProfilePolicies.mockResolvedValueOnce([]);
    const { wrapper } = createWrapper();

    renderHook(() => useApplications({ page: 1, pageSize: 20, search: 'orders' }), { wrapper });
    renderHook(() => useApplication('app-1'), { wrapper });
    renderHook(() => useApplicationCredentials('app-1'), { wrapper });
    renderHook(() => useApplicationProfilePolicies(), { wrapper });

    await waitFor(() => expect(api.getApplications).toHaveBeenCalledWith({ page: 1, pageSize: 20, search: 'orders' }));
    expect(api.getApplication).toHaveBeenCalledWith('app-1');
    expect(api.getApplicationCredentials).toHaveBeenCalledWith('app-1');
    expect(api.getApplicationProfilePolicies).toHaveBeenCalledTimes(1);
  });

  it('invalidates application queries after mutations', async () => {
    api.createApplication.mockResolvedValueOnce({ id: 'app-1' });
    api.updateApplicationMetadata.mockResolvedValueOnce({ id: 'app-1' });
    api.disableApplication.mockResolvedValueOnce({ id: 'app-1' });
    api.enableApplication.mockResolvedValueOnce({ id: 'app-1' });
    api.addApplicationSecret.mockResolvedValueOnce({ credentialId: 'cred-1', clientSecret: 'secret' });
    api.revokeApplicationCredential.mockResolvedValueOnce(undefined);
    api.deleteApplication.mockResolvedValueOnce(undefined);
    const { queryClient, wrapper } = createWrapper();
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    const { result: createResult } = renderHook(() => useCreateApplication(), { wrapper });
    await createResult.current.mutateAsync({
      clientId: 'orders-web',
      displayName: 'Orders Web',
      profile: ApplicationProfile.Web,
      clientType: ApplicationClientType.Confidential,
      allowedGrantTypes: ['authorization_code'],
      allowedScopes: ['openid'],
      redirectUris: [],
      postLogoutRedirectUris: [],
      requirePkce: true,
      requireConsent: true,
    });

    const { result: updateResult } = renderHook(() => useUpdateApplication('app-1'), { wrapper });
    await updateResult.current.mutateAsync({ displayName: 'Orders Admin', description: null });

    const { result: disableResult } = renderHook(() => useDisableApplication('app-1'), { wrapper });
    await disableResult.current.mutateAsync();

    const { result: enableResult } = renderHook(() => useEnableApplication('app-1'), { wrapper });
    await enableResult.current.mutateAsync();

    const { result: secretResult } = renderHook(() => useAddApplicationSecret('app-1'), { wrapper });
    await secretResult.current.mutateAsync({ revokeExisting: true });

    const { result: revokeResult } = renderHook(() => useRevokeApplicationCredential('app-1'), { wrapper });
    await revokeResult.current.mutateAsync('cred-1');

    const { result: deleteResult } = renderHook(() => useDeleteApplication('app-1'), { wrapper });
    await deleteResult.current.mutateAsync();

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: [APPLICATIONS_QUERY_KEY] });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: [APPLICATIONS_QUERY_KEY, 'app-1'] });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: [APPLICATIONS_QUERY_KEY, 'app-1', 'credentials'] });
  });
});
