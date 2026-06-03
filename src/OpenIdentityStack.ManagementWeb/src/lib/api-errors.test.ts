import { describe, expect, it } from 'vitest';
import { createApiError, formatApiError, getValidationErrors, isApiError } from './api-errors';

describe('api error helpers', () => {
  it('creates normalized api errors from problem details payloads', () => {
    const error = createApiError(422, {
      type: 'https://example.com/validation',
      title: 'Validation failed',
      detail: 'Check the request.',
      errors: {
        DisplayName: ['Required'],
        Email: ['Invalid format'],
      },
      errorCode: 'validation_failed',
    });

    expect(isApiError(error)).toBe(true);
    expect(error).toMatchObject({
      type: 'https://example.com/validation',
      title: 'Validation failed',
      status: 422,
      detail: 'Check the request.',
      errorCode: 'validation_failed',
      errors: {
        DisplayName: ['Required'],
        Email: ['Invalid format'],
      },
    });
  });

  it('falls back to stable messages for non-problem payloads', () => {
    const error = createApiError(500, 'not json');

    expect(error.title).toBe('Admin API request failed');
    expect(error.detail).toBe('Admin API request failed with status 500');
    expect(formatApiError(error)).toBe('Admin API request failed with status 500');
  });

  it('formats validation errors for display and form binding', () => {
    const error = createApiError(400, {
      title: 'Validation failed',
      detail: 'Check the request.',
      errors: { DisplayName: ['Required', 'Too short'] },
    });

    expect(formatApiError(error)).toBe('Check the request.\nDisplayName: Required, Too short');
    expect(getValidationErrors(error)).toEqual({ DisplayName: ['Required', 'Too short'] });
    expect(getValidationErrors(new Error('Nope'))).toEqual({});
  });
});
