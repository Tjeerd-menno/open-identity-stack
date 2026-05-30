/**
 * T060-T061: Add API client axios interceptor for Bearer token injection and 401 handling
 * 
 * Base API client with Axios
 */
import axios, { type AxiosInstance, AxiosError, type InternalAxiosRequestConfig } from 'axios';
import type { ApiError } from '@/types';
import { getRuntimeConfig } from '@/config/runtime-config';

type ApiErrorPayload = Partial<ApiError> & {
  message?: string;
  error?: string;
};

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null;

const toApiErrorPayload = (value: unknown): ApiErrorPayload | null => {
  if (!isRecord(value)) {
    return null;
  }

  const errors = value.errors;
  return {
    type: typeof value.type === 'string' ? value.type : undefined,
    title: typeof value.title === 'string' ? value.title : undefined,
    detail: typeof value.detail === 'string' ? value.detail : undefined,
    message: typeof value.message === 'string' ? value.message : undefined,
    errorCode:
      typeof value.errorCode === 'string'
      ? value.errorCode
      : typeof value.error === 'string'
        ? value.error
        : undefined,
    errors: isRecord(errors)
      ? Object.fromEntries(
          Object.entries(errors).filter(
            (entry): entry is [string, string[]] =>
              Array.isArray(entry[1]) && entry[1].every((item) => typeof item === 'string')
          )
        )
      : undefined,
  };
};

/**
 * Base API client with Axios
 */
class ApiClient {
  private readonly client: AxiosInstance;
  private tokenProvider: (() => Promise<string | null>) | null = null;
  private logoutHandler: (() => void) | null = null;

  constructor() {
    this.client = axios.create({
      baseURL: getRuntimeConfig('VITE_API_BASE_URL', 'http://localhost:5000'),
      headers: {
        'Content-Type': 'application/json',
      },
      timeout: 30000,
    });

    this.setupInterceptors();
  }

  /**
   * T060: Set the token provider function
   * Allows the auth context to provide access tokens dynamically
   */
  setTokenProvider(provider: () => Promise<string | null>) {
    this.tokenProvider = provider;
  }

  /**
   * T061: Set the logout handler function
   * Called when a 401 response is received
   */
  setLogoutHandler(handler: () => void) {
    this.logoutHandler = handler;
  }

  /**
   * Set up request and response interceptors
   */
  private setupInterceptors() {
    // T060: Request interceptor: inject access token
    this.client.interceptors.request.use(
      async (config: InternalAxiosRequestConfig) => {
        if (this.tokenProvider) {
          const token = await this.tokenProvider();
          if (token && config.headers) {
            config.headers.Authorization = `Bearer ${token}`;
          }
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    // T061: Response interceptor: handle errors and 401 unauthorized
    this.client.interceptors.response.use(
      (response) => response,
      (error: AxiosError<ApiErrorPayload>) => {
        // Handle 401 Unauthorized - trigger logout
        if (error.response?.status === 401) {
          console.warn('Received 401 Unauthorized response, logging out...');
          if (this.logoutHandler) {
            this.logoutHandler();
          }
        }
        
        const apiError = this.handleError(error);
        return Promise.reject(Object.assign(new Error(apiError.detail ?? apiError.title), apiError));
      }
    );
  }

  /**
   * Handle and normalize API errors
   */
  private handleError(error: AxiosError<ApiErrorPayload>): ApiError {
    if (error.response) {
      // Server responded with error status
      const data = toApiErrorPayload(error.response.data);
      return {
        type: data?.type || 'https://tools.ietf.org/html/rfc7231#section-6.5.1',
        title: data?.title || 'An error occurred',
        status: error.response.status,
        detail: data?.detail || data?.message || error.message,
        errors: data?.errors,
        errorCode: data?.errorCode || data?.error,
      };
    } else if (error.request) {
      // Request made but no response
      return {
        type: 'network-error',
        title: 'Network Error',
        status: 0,
        detail: 'Unable to reach the server. Please check your connection.',
      };
    } else {
      // Something else happened
      return {
        type: 'unknown-error',
        title: 'Unknown Error',
        status: 0,
        detail: error.message || 'An unexpected error occurred',
      };
    }
  }

  /**
   * GET request
   */
  async get<T, TParams extends object | undefined = object | undefined>(
    url: string,
    params?: TParams
  ): Promise<T> {
    const response = await this.client.get<T>(url, { params });
    return response.data;
  }

  /**
   * POST request
   */
  async post<T, TBody = unknown>(url: string, data?: TBody): Promise<T> {
    const response = await this.client.post<T>(url, data);
    return response.data;
  }

  /**
   * PUT request
   */
  async put<T, TBody = unknown>(url: string, data?: TBody): Promise<T> {
    const response = await this.client.put<T>(url, data);
    return response.data;
  }

  /**
   * PATCH request
   */
  async patch<T, TBody = unknown>(url: string, data?: TBody): Promise<T> {
    const response = await this.client.patch<T>(url, data);
    return response.data;
  }

  /**
   * DELETE request
   */
  async delete<T>(url: string): Promise<T> {
    const response = await this.client.delete<T>(url);
    return response.data;
  }
}

// Export singleton instance
export const apiClient = new ApiClient();
export default apiClient;
