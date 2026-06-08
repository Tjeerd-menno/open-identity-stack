import {
  createAdminApiClient,
  type AdminApiClient,
  type RequestParams,
} from '@openidentitystack/admin-api-client';
import { getRuntimeConfig } from '@/config/runtime-config';

class ApiClient {
  private tokenProvider: (() => Promise<string | null>) | null = null;
  private logoutHandler: (() => void) | null = null;

  private readonly client = createAdminApiClient({
    baseUrl: () => getRuntimeConfig('VITE_API_BASE_URL', 'http://localhost:5000'),
    getAccessToken: () => (this.tokenProvider ? this.tokenProvider() : Promise.resolve(null)),
    onUnauthorized: () => this.logoutHandler?.(),
  });

  setTokenProvider(provider: () => Promise<string | null>): void {
    this.tokenProvider = provider;
  }

  setLogoutHandler(handler: (() => void) | null): void {
    this.logoutHandler = handler;
  }

  async get<T, TParams extends object | undefined = object | undefined>(
    url: string,
    params?: TParams
  ): Promise<T> {
    return this.client.get<T>(url, params as RequestParams | undefined);
  }

  async post<T, TBody = unknown>(url: string, data?: TBody): Promise<T> {
    return this.client.post<T, TBody>(url, data);
  }

  async put<T, TBody = unknown>(url: string, data?: TBody): Promise<T> {
    return this.client.put<T, TBody>(url, data);
  }

  async patch<T, TBody = unknown>(url: string, data?: TBody): Promise<T> {
    return this.client.patch<T, TBody>(url, data);
  }

  async delete<T>(url: string): Promise<T> {
    return this.client.delete<T>(url);
  }
}

export const apiClient = new ApiClient();
export default apiClient;

function requestBodyToPayload(body: RequestInit['body']): unknown | undefined {
  if (body === undefined) {
    return undefined;
  }

  if (typeof body !== 'string') {
    return body;
  }

  try {
    return JSON.parse(body);
  } catch {
    return body;
  }
}

function requestMethod(method: string): string {
  return method.toUpperCase();
}

export const adminApiClient: AdminApiClient = {
  request(path, options = {}, params) {
    const method = options.method ? requestMethod(options.method) : 'GET';
    const payload = requestBodyToPayload(options.body);

    if (method === 'GET') {
      return apiClient.get(path, params);
    }

    if (method === 'POST') {
      return apiClient.post(path, payload);
    }

    if (method === 'PUT') {
      return apiClient.put(path, payload);
    }

    if (method === 'PATCH') {
      return apiClient.patch(path, payload);
    }

    if (method === 'DELETE') {
      return apiClient.delete(path);
    }

    return apiClient.delete(path);
  },
  get<T>(path, params) {
    return apiClient.get<T>(path, params as RequestParams | undefined);
  },
  post<T, TBody = unknown>(path, data) {
    return apiClient.post<T, TBody>(path, data);
  },
  put<T, TBody = unknown>(path, data) {
    return apiClient.put<T, TBody>(path, data);
  },
  patch<T, TBody = unknown>(path, data) {
    return apiClient.patch<T, TBody>(path, data);
  },
  delete<T>(path) {
    return apiClient.delete<T>(path);
  },
};
