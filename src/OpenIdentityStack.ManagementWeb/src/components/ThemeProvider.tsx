/* eslint-disable react-refresh/only-export-components */
import { MantineProvider } from '@mantine/core';
import { Notifications } from '@mantine/notifications';
import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  getStoredThemePreference,
  getSystemThemePreference,
  resolveThemePreference,
  setStoredThemePreference,
  type ResolvedTheme,
  type ThemePreference,
} from '@/lib/theme-preference';

type ThemePreferenceContextValue = {
  preference: ThemePreference;
  resolvedTheme: ResolvedTheme;
  setPreference: (preference: ThemePreference) => void;
};

const ThemePreferenceContext = createContext<ThemePreferenceContextValue | null>(null);

export function useThemePreference(): ThemePreferenceContextValue {
  const context = useContext(ThemePreferenceContext);
  if (!context) {
    throw new Error('useThemePreference must be used within ManagementThemeProvider');
  }

  return context;
}

type ManagementThemeProviderProps = {
  children: ReactNode;
};

export function ManagementThemeProvider({ children }: ManagementThemeProviderProps) {
  const [preference, setPreferenceState] = useState<ThemePreference>(() => getStoredThemePreference());
  const [systemPreference, setSystemPreference] = useState<ResolvedTheme>(() => getSystemThemePreference());
  const resolvedTheme = resolveThemePreference(preference, systemPreference);

  useEffect(() => {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const listener = () => setSystemPreference(mediaQuery.matches ? 'dark' : 'light');

    mediaQuery.addEventListener('change', listener);
    return () => mediaQuery.removeEventListener('change', listener);
  }, []);

  useEffect(() => {
    document.documentElement.setAttribute('data-mantine-color-scheme', resolvedTheme);
  }, [resolvedTheme]);

  const value = useMemo<ThemePreferenceContextValue>(
    () => ({
      preference,
      resolvedTheme,
      setPreference: (nextPreference) => {
        setStoredThemePreference(nextPreference);
        setPreferenceState(nextPreference);
      },
    }),
    [preference, resolvedTheme]
  );

  return (
    <ThemePreferenceContext.Provider value={value}>
      <MantineProvider defaultColorScheme="auto" forceColorScheme={resolvedTheme}>
        <Notifications position="top-right" />
        {children}
      </MantineProvider>
    </ThemePreferenceContext.Provider>
  );
}
