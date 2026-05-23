import { beforeEach, describe, expect, it, vi } from 'vitest';

const mockedAxios = vi.hoisted(() => {
  let requestFulfilled: ((config: any) => any) | undefined;
  let responseRejected: ((error: any) => Promise<never>) | undefined;

  const instance = {
    interceptors: {
      request: {
        use: vi.fn((onFulfilled: (config: any) => any) => {
          requestFulfilled = onFulfilled;
          return 0;
        }),
      },
      response: {
        use: vi.fn((_onFulfilled: (value: any) => any, onRejected: (error: any) => Promise<never>) => {
          responseRejected = onRejected;
          return 0;
        }),
      },
    },
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn(),
  };

  return {
    create: vi.fn(() => instance),
    instance,
    getRequestFulfilled: () => requestFulfilled,
    getResponseRejected: () => responseRejected,
  };
});

vi.mock('axios', () => ({
  __esModule: true,
  AxiosError: class AxiosError extends Error {},
  default: {
    create: mockedAxios.create,
  },
}));

vi.mock('@/config/runtime-config', () => ({
  getRuntimeConfig: (_key: string, fallback = '') => fallback,
}));

import { apiClient } from './client';

describe('ApiClient', () => {
  beforeEach(() => {
    mockedAxios.instance.get.mockReset();
    mockedAxios.instance.post.mockReset();
    mockedAxios.instance.put.mockReset();
    mockedAxios.instance.patch.mockReset();
    mockedAxios.instance.delete.mockReset();
  });

  it('injects a bearer token via the request interceptor after setting a token provider', async () => {
    const tokenProvider = vi.fn().mockResolvedValue('token-123');
    apiClient.setTokenProvider(tokenProvider);

    const requestFulfilled = mockedAxios.getRequestFulfilled();
    const config = await requestFulfilled?.({ headers: {} });

    expect(tokenProvider).toHaveBeenCalledOnce();
    expect(config?.headers.Authorization).toBe('Bearer token-123');
  });

  it('triggers the logout handler and normalizes 401 errors in the response interceptor', async () => {
    const logoutHandler = vi.fn();
    apiClient.setLogoutHandler(logoutHandler);

    const responseRejected = mockedAxios.getResponseRejected();
    const error = {
      response: {
        status: 401,
        data: {
          title: 'Unauthorized',
          detail: 'Token expired',
          type: 'https://example.com/errors/unauthorized',
        },
      },
      message: 'Request failed with status code 401',
    };

    await expect(responseRejected?.(error)).rejects.toMatchObject({
      title: 'Unauthorized',
      detail: 'Token expired',
      status: 401,
      message: 'Token expired',
    });
    expect(logoutHandler).toHaveBeenCalledOnce();
  });

  it('proxies HTTP verbs to the underlying axios instance', async () => {
    mockedAxios.instance.get.mockResolvedValue({ data: { items: [1] } });
    mockedAxios.instance.post.mockResolvedValue({ data: { id: 'created' } });
    mockedAxios.instance.put.mockResolvedValue({ data: { id: 'updated' } });
    mockedAxios.instance.patch.mockResolvedValue({ data: { id: 'patched' } });
    mockedAxios.instance.delete.mockResolvedValue({ data: { success: true } });

    await expect(apiClient.get('/users', { page: 2 })).resolves.toEqual({ items: [1] });
    await expect(apiClient.post('/users', { name: 'Alice' })).resolves.toEqual({ id: 'created' });
    await expect(apiClient.put('/users/1', { name: 'Bob' })).resolves.toEqual({ id: 'updated' });
    await expect(apiClient.patch('/users/1', { active: true })).resolves.toEqual({ id: 'patched' });
    await expect(apiClient.delete('/users/1')).resolves.toEqual({ success: true });

    expect(mockedAxios.instance.get).toHaveBeenCalledWith('/users', { params: { page: 2 } });
    expect(mockedAxios.instance.post).toHaveBeenCalledWith('/users', { name: 'Alice' });
    expect(mockedAxios.instance.put).toHaveBeenCalledWith('/users/1', { name: 'Bob' });
    expect(mockedAxios.instance.patch).toHaveBeenCalledWith('/users/1', { active: true });
    expect(mockedAxios.instance.delete).toHaveBeenCalledWith('/users/1');
  });
});
