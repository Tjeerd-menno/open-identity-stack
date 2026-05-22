import { describe, expect, it } from 'vitest';
import {
  formatApiError,
  getUserFriendlyError,
  getValidationErrors,
  isConflictError,
  isForbiddenError,
  isNotFoundError,
  isUnauthorizedError,
  isValidationError,
} from './error-handler';

const apiError = (status: number, overrides = {}) => ({
  type: 'https://example.com/error',
  title: 'Request failed',
  status,
  ...overrides,
});

describe('error handler', () => {
  it('formats validation details with field messages', () => {
    expect(
      formatApiError(
        apiError(422, {
          title: 'Validation failed',
          errors: {
            DisplayName: ['Required', 'Too short'],
            Email: ['Invalid'],
          },
        })
      )
    ).toBe('Validation failed\nDisplayName: Required, Too short\nEmail: Invalid');
  });

  it('falls back through API detail, Error messages, and unknown values', () => {
    expect(formatApiError(apiError(500, { detail: 'Database offline' }))).toBe('Database offline');
    expect(formatApiError(new Error('Client-side failure'))).toBe('Client-side failure');
    expect(formatApiError('nope')).toBe('An unexpected error occurred');
  });

  it.each([
    [400, 'Invalid request. Please check your input.'],
    [401, 'You are not authenticated. Please log in.'],
    [403, 'You do not have permission to perform this action.'],
    [404, 'The requested resource was not found.'],
    [409, 'This action conflicts with existing data.'],
    [422, 'Validation failed. Please check your input.'],
    [500, 'A server error occurred. Please try again later.'],
  ])('returns friendly message for status %i', (status, message) => {
    expect(getUserFriendlyError(apiError(status))).toBe(message);
  });

  it('uses API detail or title for unrecognized statuses', () => {
    expect(getUserFriendlyError(apiError(418, { detail: 'Teapot mode' }))).toBe('Teapot mode');
    expect(getUserFriendlyError(apiError(429))).toBe('Request failed');
  });

  it('converts validation field names to camelCase', () => {
    expect(
      getValidationErrors(
        apiError(422, {
          errors: {
            DisplayName: ['Required'],
            ClientId: ['Must be unique'],
          },
        })
      )
    ).toEqual({
      displayName: 'Required',
      clientId: 'Must be unique',
    });
  });

  it('returns empty validation errors for non-API errors', () => {
    expect(getValidationErrors(new Error('not an API error'))).toEqual({});
  });

  it('recognizes common error status helpers', () => {
    expect(isUnauthorizedError(apiError(401))).toBe(true);
    expect(isForbiddenError(apiError(403))).toBe(true);
    expect(isNotFoundError(apiError(404))).toBe(true);
    expect(isConflictError(apiError(409))).toBe(true);
    expect(isValidationError(apiError(422))).toBe(true);
    expect(isUnauthorizedError(apiError(403))).toBe(false);
  });
});
