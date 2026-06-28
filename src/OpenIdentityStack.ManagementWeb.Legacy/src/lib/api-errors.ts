export type ApiError = Error & {
  type: string;
  title: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>;
  errorCode?: string;
};

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
