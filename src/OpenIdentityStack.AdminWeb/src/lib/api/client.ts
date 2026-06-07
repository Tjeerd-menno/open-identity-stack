import {
  createAdminApiClient,
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
