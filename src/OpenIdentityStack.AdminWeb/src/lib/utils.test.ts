import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  buildQueryString,
  cn,
  copyToClipboard,
  debounce,
  formatDate,
  formatDateTime,
  formatNumber,
  formatRelativeTime,
  isEmpty,
  parseSearchParams,
  pluralize,
  randomString,
  sleep,
  truncate,
} from './utils';

describe('utils', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-05-22T12:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('merges conditional classes and resolves Tailwind conflicts', () => {
    const shouldHide = false;

    expect(cn('px-2', shouldHide && 'hidden', ['text-sm', 'px-4'])).toBe('text-sm px-4');
  });

  it('formats empty, string, and Date values', () => {
    const localMay22Morning = new Date(2026, 4, 22, 10, 30, 0);
    const localJan2Noon = new Date(2026, 0, 2, 12, 0, 0);

    expect(formatDate(null)).toBe('N/A');
    expect(formatDate(localMay22Morning)).toBe('May 22, 2026');
    expect(formatDate(localJan2Noon)).toBe('Jan 2, 2026');
    expect(formatDateTime(null)).toBe('N/A');
    expect(formatDateTime(localMay22Morning)).toContain('May 22, 2026');
  });

  it('formats relative times across minute, hour, day, and older ranges', () => {
    expect(formatRelativeTime(null)).toBe('N/A');
    expect(formatRelativeTime('2026-05-22T11:59:45Z')).toBe('Just now');
    expect(formatRelativeTime('2026-05-22T11:58:00Z')).toBe('2 minutes ago');
    expect(formatRelativeTime('2026-05-22T10:00:00Z')).toBe('2 hours ago');
    expect(formatRelativeTime('2026-05-20T12:00:00Z')).toBe('2 days ago');
    expect(formatRelativeTime('2026-03-01T12:00:00Z')).toBe('Mar 1, 2026');
  });

  it('truncates only when text exceeds the requested length', () => {
    expect(truncate('short', 10)).toBe('short');
    expect(truncate('longer than ten', 10)).toBe('longer ...');
  });

  it('pluralizes regular and irregular labels', () => {
    expect(pluralize(1, 'user')).toBe('user');
    expect(pluralize(2, 'user')).toBe('users');
    expect(pluralize(2, 'person', 'people')).toBe('people');
  });

  it('formats numbers and query strings', () => {
    expect(formatNumber(1234567)).toBe('1,234,567');
    expect(parseSearchParams('?page=2&search=alice')).toEqual({
      page: '2',
      search: 'alice',
    });
    expect(buildQueryString({ page: 2, empty: '', missing: null, term: 'alice' })).toBe(
      '?page=2&term=alice'
    );
    expect(buildQueryString({ empty: undefined })).toBe('');
  });

  it('copies to the clipboard when available', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });

    await expect(copyToClipboard('secret')).resolves.toBe(true);

    expect(writeText).toHaveBeenCalledWith('secret');
  });

  it('returns false when clipboard writes fail', async () => {
    const writeText = vi.fn().mockRejectedValue(new Error('denied'));
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });

    await expect(copyToClipboard('secret')).resolves.toBe(false);

    expect(consoleError).toHaveBeenCalled();
  });

  it('generates random strings from crypto bytes', () => {
    const getRandomValues = vi
      .spyOn(crypto, 'getRandomValues')
      .mockImplementation((array) => {
        const values = array as Uint32Array;
        for (let i = 0; i < values.length; i += 1) {
          values[i] = i;
        }
        return array;
      });

    expect(randomString(5)).toBe('ABCDE');
    expect(getRandomValues).toHaveBeenCalledWith(expect.any(Uint32Array));
  });

  it('sleeps for the requested timeout', async () => {
    const promise = sleep(250);
    vi.advanceTimersByTime(249);
    let resolved = false;
    promise.then(() => {
      resolved = true;
    });
    await Promise.resolve();
    expect(resolved).toBe(false);

    vi.advanceTimersByTime(1);
    await expect(promise).resolves.toBeUndefined();
  });

  it('debounces repeated calls and keeps the last arguments', () => {
    const callback = vi.fn();
    const debounced = debounce(callback, 100);

    debounced('first');
    debounced('second');
    vi.advanceTimersByTime(99);
    expect(callback).not.toHaveBeenCalled();

    vi.advanceTimersByTime(1);
    expect(callback).toHaveBeenCalledOnce();
    expect(callback).toHaveBeenCalledWith('second');
  });

  it('detects empty values without treating booleans or numbers as empty', () => {
    expect(isEmpty(null)).toBe(true);
    expect(isEmpty(undefined)).toBe(true);
    expect(isEmpty('   ')).toBe(true);
    expect(isEmpty([])).toBe(true);
    expect(isEmpty({})).toBe(true);
    expect(isEmpty(false)).toBe(false);
    expect(isEmpty(0)).toBe(false);
    expect(isEmpty(['value'])).toBe(false);
  });
});
