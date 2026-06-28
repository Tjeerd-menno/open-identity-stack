import { describe, expect, it } from 'vitest';
import { formatCount, formatDateTime, formatRelativeTime, getInitials } from './format';

describe('getInitials', () => {
  it('takes the first letter of the first two words', () => {
    expect(getInitials('Ada Lovelace')).toBe('AL');
    expect(getInitials('grace brewster hopper')).toBe('GB');
  });

  it('handles a single name', () => {
    expect(getInitials('madonna')).toBe('M');
  });
});

describe('formatCount', () => {
  it('groups thousands with commas', () => {
    expect(formatCount(1284)).toBe('1,284');
    expect(formatCount(5)).toBe('5');
  });
});

describe('formatRelativeTime', () => {
  it('returns "Never" for empty values', () => {
    expect(formatRelativeTime(null)).toBe('Never');
    expect(formatRelativeTime(undefined)).toBe('Never');
  });

  it('renders a relative minute string for recent times', () => {
    const fiveMinutesAgo = new Date(Date.now() - 5 * 60 * 1000);
    expect(formatRelativeTime(fiveMinutesAgo)).toMatch(/minute/);
  });

  it('renders an em-dash for invalid input', () => {
    expect(formatRelativeTime('not-a-date')).toBe('—');
  });
});

describe('formatDateTime', () => {
  it('returns an em-dash for empty values', () => {
    expect(formatDateTime(null)).toBe('—');
    expect(formatDateTime('')).toBe('—');
  });
});
