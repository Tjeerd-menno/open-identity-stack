export type ThemePreference = 'light' | 'dark' | 'system';
export type ResolvedTheme = 'light' | 'dark';

export const themePreferenceStorageKey = 'openidentitystack.management.theme';

const supportedPreferences = new Set<ThemePreference>(['light', 'dark', 'system']);

export function getStoredThemePreference(storage: Storage = localStorage): ThemePreference {
  try {
    const value = storage.getItem(themePreferenceStorageKey);
    return supportedPreferences.has(value as ThemePreference) ? (value as ThemePreference) : 'system';
  } catch {
    return 'system';
  }
}

export function setStoredThemePreference(preference: ThemePreference, storage: Storage = localStorage): void {
  try {
    storage.setItem(themePreferenceStorageKey, preference);
  } catch {
    // Appearance preference is non-critical; keep the app usable when storage is blocked.
  }
}

export function getSystemThemePreference(): ResolvedTheme {
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

export function resolveThemePreference(
  preference: ThemePreference,
  systemPreference: ResolvedTheme = getSystemThemePreference()
): ResolvedTheme {
  return preference === 'system' ? systemPreference : preference;
}
