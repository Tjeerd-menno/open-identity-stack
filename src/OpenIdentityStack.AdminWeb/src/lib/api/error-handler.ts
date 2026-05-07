import { isApiError } from '@/types';

/**
 * Format API error for display
 */
export function formatApiError(error: unknown): string {
  if (isApiError(error)) {
    if (error.errors && Object.keys(error.errors).length > 0) {
      // Validation errors
      const errorMessages = Object.entries(error.errors)
        .map(([field, messages]) => `${field}: ${messages.join(', ')}`)
        .join('\n');
      return `${error.title}\n${errorMessages}`;
    }
    return error.detail || error.title;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return 'An unexpected error occurred';
}

/**
 * Get user-friendly error message
 */
export function getUserFriendlyError(error: unknown): string {
  if (isApiError(error)) {
    switch (error.status) {
      case 400:
        return 'Invalid request. Please check your input.';
      case 401:
        return 'You are not authenticated. Please log in.';
      case 403:
        return 'You do not have permission to perform this action.';
      case 404:
        return 'The requested resource was not found.';
      case 409:
        return 'This action conflicts with existing data.';
      case 422:
        return 'Validation failed. Please check your input.';
      case 500:
        return 'A server error occurred. Please try again later.';
      default:
        return error.detail || error.title || 'An error occurred';
    }
  }

  return formatApiError(error);
}

/**
 * Extract validation errors for form fields
 */
export function getValidationErrors(error: unknown): Record<string, string> {
  if (isApiError(error) && error.errors) {
    const fieldErrors: Record<string, string> = {};
    
    Object.entries(error.errors).forEach(([field, messages]) => {
      // Convert field name from PascalCase to camelCase
      const camelField = field.charAt(0).toLowerCase() + field.slice(1);
      fieldErrors[camelField] = messages[0]; // Take first error message
    });

    return fieldErrors;
  }

  return {};
}

/**
 * Check if error is a specific HTTP status code
 */
export function isErrorStatus(error: unknown, status: number): boolean {
  return isApiError(error) && error.status === status;
}

/**
 * Check if error is unauthorized (401)
 */
export function isUnauthorizedError(error: unknown): boolean {
  return isErrorStatus(error, 401);
}

/**
 * Check if error is forbidden (403)
 */
export function isForbiddenError(error: unknown): boolean {
  return isErrorStatus(error, 403);
}

/**
 * Check if error is not found (404)
 */
export function isNotFoundError(error: unknown): boolean {
  return isErrorStatus(error, 404);
}

/**
 * Check if error is conflict (409)
 */
export function isConflictError(error: unknown): boolean {
  return isErrorStatus(error, 409);
}

/**
 * Check if error is validation error (422)
 */
export function isValidationError(error: unknown): boolean {
  return isErrorStatus(error, 422);
}
