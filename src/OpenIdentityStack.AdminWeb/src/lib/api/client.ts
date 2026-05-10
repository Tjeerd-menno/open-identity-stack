/**
 * T060-T061: Add API client axios interceptor for Bearer token injection and 401 handling
 * 
 * Base API client with Axios
 */
import axios, { type AxiosInstance, AxiosError, type InternalAxiosRequestConfig } from 'axios';
import type { ApiError } from '@/types';
import { getRuntimeConfig } from '@/config/runtime-config';

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
      (error: AxiosError) => {
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
  private handleError(error: AxiosError): ApiError {
    if (error.response) {
      // Server responded with error status
      const data = error.response.data as any;
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
  async get<T>(url: string, params?: any): Promise<T> {
    const response = await this.client.get<T>(url, { params });
    return response.data;
  }

  /**
   * POST request
   */
  async post<T>(url: string, data?: any): Promise<T> {
    const response = await this.client.post<T>(url, data);
    return response.data;
  }

  /**
   * PUT request
   */
  async put<T>(url: string, data?: any): Promise<T> {
    const response = await this.client.put<T>(url, data);
    return response.data;
  }

  /**
   * PATCH request
   */
  async patch<T>(url: string, data?: any): Promise<T> {
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
