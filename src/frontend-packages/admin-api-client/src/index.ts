export type RequestParams = Record<string, string | number | boolean | undefined>;

export type AdminApiClientOptions = {
  baseUrl: string | (() => string);
  getAccessToken?: () => Promise<string | null>;
  onUnauthorized?: () => void;
  onAdministrativeApprovalRequired?: (error: ApiError) => Promise<boolean>;
};

export type ApiError = Error & {
  type: string;
  title: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>;
  errorCode?: string;
};

export type PaginatedResponse<T> = {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

export type AdminApiClient = {
  request<T>(path: string, options?: RequestInit, params?: RequestParams): Promise<T>;
  get<T>(path: string, params?: RequestParams): Promise<T>;
  post<T, TBody = unknown>(path: string, body?: TBody): Promise<T>;
  put<T, TBody = unknown>(path: string, body?: TBody): Promise<T>;
  patch<T, TBody = unknown>(path: string, body?: TBody): Promise<T>;
  delete<T>(path: string, params?: RequestParams): Promise<T>;
};

export function createAdminApiClient(options: AdminApiClientOptions): AdminApiClient {
  async function request<T>(
    path: string,
    requestOptions: RequestInit = {},
    params?: RequestParams,
    approvalAttempted = false
  ): Promise<T> {
    const headers = new Headers(requestOptions.headers);
    headers.set('Content-Type', 'application/json');

    const token = options.getAccessToken ? await options.getAccessToken() : null;
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }

    let response: Response;
    try {
      response = await fetch(buildUrl(resolveBaseUrl(options.baseUrl), path, params), {
        ...requestOptions,
        headers,
      });
    } catch (error) {
      throw createNetworkError(error);
    }

    if (!response.ok) {
      if (response.status === 401) {
        options.onUnauthorized?.();
      }

      const error = createApiError(response.status, await readErrorPayload(response));
      const protectedMutationDenied = requestOptions.method && requestOptions.method !== 'GET' && response.status === 403;
      if (protectedMutationDenied && error.errorCode === 'Forbidden.AdministrativeApproval.ReauthenticationRequired') {
        // Authentication may expire while acknowledgement is pending. Offer sign-in even
        // after that retry, but let the operator start a new mutation after authenticating.
        await options.onAdministrativeApprovalRequired?.(error);
        throw error;
      }
      if (!approvalAttempted && protectedMutationDenied
        && error.errorCode === 'Forbidden.AdministrativeApproval.AcknowledgementRequired'
        && await options.onAdministrativeApprovalRequired?.(error)) {
        const approvedHeaders = new Headers(requestOptions.headers);
        approvedHeaders.set('X-OIS-Administrative-Approval', 'acknowledge');
        return request<T>(path, { ...requestOptions, headers: approvedHeaders }, params, true);
      }
      throw error;
    }

    if (response.status === 204) {
      return undefined as T;
    }

    const body = await response.text();
    if (!body) {
      return undefined as T;
    }

    return JSON.parse(body) as T;
  }

  return {
    request,
    get: <T>(path: string, params?: RequestParams) => request<T>(path, {}, params),
    post: <T, TBody = unknown>(path: string, body?: TBody) =>
      request<T>(path, jsonRequest('POST', body)),
    put: <T, TBody = unknown>(path: string, body?: TBody) =>
      request<T>(path, jsonRequest('PUT', body)),
    patch: <T, TBody = unknown>(path: string, body?: TBody) =>
      request<T>(path, jsonRequest('PATCH', body)),
    delete: <T>(path: string, params?: RequestParams) => request<T>(path, { method: 'DELETE' }, params),
  };
}

export { createUsersContract } from './users';
export * from './administrative-access';
export type {
  UserStatus,
  UserListItem,
  UserProfile,
  User,
  RoleListItem as UserRoleListItem,
  UserGroup,
  UpstreamIdentity,
  UserListParams,
  CreateUserRequest,
  UpdateUserRequest,
  DisableUserRequest,
  ResetPasswordRequest,
  UserStatusChangeResponse,
  PasswordResetResponse,
  UsersContract,
} from './users';
export { createGroupsContract } from './groups';
export * from './groups';
export { createApplicationsContract } from './applications';
export * from './applications';
export { createRolesContract } from './roles';
export * from './roles';
export { createSessionsContract } from './sessions';
export * from './sessions';
export { createProvidersContract } from './providers';
export * from './providers';
export { createSettingsContract } from './settings';
export * from './settings';
export * from './cutover';
export { createAuditEntriesContract } from './audit-entries';
export * from './audit-entries';
export { createApplicationPermissionsContract } from './application-permissions';
export * from './application-permissions';
export { createCurrentUserContract } from './current-user';
export * from './current-user';
export * from './permission-semantics';

export function createApiError(status: number, payload: unknown): ApiError {
  const problem = isRecord(payload) ? payload : {};
  const errors = normalizeValidationErrors(problem.errors);
  const title = typeof problem.title === 'string' ? problem.title : 'Admin API request failed';
  const detail =
    typeof problem.detail === 'string'
      ? problem.detail
      : typeof problem.message === 'string'
        ? problem.message
        : `Admin API request failed with status ${status}`;

  return Object.assign(new Error(detail), {
    type: typeof problem.type === 'string' ? problem.type : 'about:blank',
    title,
    status,
    detail,
    errors,
    errorCode:
      typeof problem.errorCode === 'string'
        ? problem.errorCode
        : typeof problem.code === 'string'
          ? problem.code
          : typeof problem.error === 'string'
            ? problem.error
            : undefined,
  });
}

export function isApiError(error: unknown): error is ApiError {
  return (
    isRecord(error) &&
    typeof (error as Partial<ApiError>).title === 'string' &&
    typeof (error as Partial<ApiError>).status === 'number'
  );
}

export function formatApiError(error: unknown): string {
  if (!isApiError(error)) {
    return error instanceof Error ? error.message : 'An unexpected error occurred.';
  }

  const validationMessages = Object.entries(getValidationErrors(error)).map(
    ([field, messages]) => `${field}: ${messages.join(', ')}`
  );

  return [error.detail ?? error.title, ...validationMessages].filter(Boolean).join('\n');
}

export function getValidationErrors(error: unknown): Record<string, string[]> {
  return isApiError(error) && error.errors ? error.errors : {};
}

function resolveBaseUrl(baseUrl: string | (() => string)): string {
  return typeof baseUrl === 'function' ? baseUrl() : baseUrl;
}

function buildUrl(baseUrl: string, path: string, params?: RequestParams): string {
  const url = new URL(path, baseUrl);

  Object.entries(params ?? {}).forEach(([key, value]) => {
    if (value !== undefined) {
      url.searchParams.set(key, String(value));
    }
  });

  return url.toString();
}

function jsonRequest<TBody>(method: string, body?: TBody): RequestInit {
  return {
    method,
    body: body === undefined ? undefined : JSON.stringify(body),
  };
}

async function readErrorPayload(response: Response): Promise<unknown> {
  const body = await response.text();
  if (!body) {
    return null;
  }

  try {
    return JSON.parse(body);
  } catch {
    return { message: body };
  }
}

function createNetworkError(error: unknown): ApiError {
  const detail = error instanceof Error ? error.message : 'Unable to reach the server.';
  return Object.assign(new Error(detail), {
    type: 'network-error',
    title: 'Network Error',
    status: 0,
    detail,
  });
}

function normalizeValidationErrors(value: unknown): Record<string, string[]> | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const entries = Object.entries(value).filter(
    (entry): entry is [string, string[]] =>
      Array.isArray(entry[1]) && entry[1].every((item) => typeof item === 'string')
  );

  return entries.length > 0 ? Object.fromEntries(entries) : undefined;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}
