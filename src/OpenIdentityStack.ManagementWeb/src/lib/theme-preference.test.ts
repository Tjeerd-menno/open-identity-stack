import {
  getStoredThemePreference,
  resolveThemePreference,
  setStoredThemePreference,
} from './theme-preference';

describe('theme preference', () => {
  it('falls back to system when no preference is stored', () => {
    expect(getStoredThemePreference()).toBe('system');
  });

  it('persists supported light, dark, and system preferences', () => {
    setStoredThemePreference('dark');
    expect(getStoredThemePreference()).toBe('dark');

    setStoredThemePreference('light');
    expect(getStoredThemePreference()).toBe('light');

    setStoredThemePreference('system');
    expect(getStoredThemePreference()).toBe('system');
  });

  it('resolves system preference from the current operating system scheme', () => {
    expect(resolveThemePreference('system', 'dark')).toBe('dark');
    expect(resolveThemePreference('system', 'light')).toBe('light');
  });

  it('falls back to system when browser storage is unavailable', () => {
    const unavailableStorage = {
      getItem: () => {
        throw new Error('storage blocked');
      },
      setItem: () => {
        throw new Error('storage blocked');
      },
    } as unknown as Storage;

    expect(getStoredThemePreference(unavailableStorage)).toBe('system');
    expect(() => setStoredThemePreference('dark', unavailableStorage)).not.toThrow();
  });
});
