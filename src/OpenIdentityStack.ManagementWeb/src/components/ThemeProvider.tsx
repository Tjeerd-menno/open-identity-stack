/* eslint-disable react-refresh/only-export-components */
import { MantineProvider, type MantineProviderProps } from '@mantine/core';
import { Notifications } from '@mantine/notifications';
import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { managementTheme } from '@/theme';

export type ThemePreference = 'light' | 'dark' | 'system';
export type ResolvedTheme = 'light' | 'dark';

const STORAGE_KEY = 'ois.mw.theme';

type ThemePreferenceContextValue = {
  preference: ThemePreference;
  resolvedTheme: ResolvedTheme;
  setPreference: (preference: ThemePreference) => void;
  toggle: () => void;
};

const ThemePreferenceContext = createContext<ThemePreferenceContextValue | null>(null);

function readStoredPreference(): ThemePreference {
  if (typeof localStorage === 'undefined') {
    return 'system';
  }
  const stored = localStorage.getItem(STORAGE_KEY);
  return stored === 'light' || stored === 'dark' || stored === 'system' ? stored : 'system';
}

function getSystemTheme(): ResolvedTheme {
  if (typeof window === 'undefined') {
    return 'light';
  }
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

export function useThemePreference(): ThemePreferenceContextValue {
  const context = useContext(ThemePreferenceContext);
  if (!context) {
    throw new Error('useThemePreference must be used within ManagementThemeProvider');
  }

  return context;
}

export function ManagementThemeProvider({ children, env }: { children: ReactNode; env?: MantineProviderProps['env'] }) {
  const [preference, setPreferenceState] = useState<ThemePreference>(() => readStoredPreference());
  const [systemTheme, setSystemTheme] = useState<ResolvedTheme>(() => getSystemTheme());
  const resolvedTheme: ResolvedTheme = preference === 'system' ? systemTheme : preference;

  useEffect(() => {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const listener = () => setSystemTheme(mediaQuery.matches ? 'dark' : 'light');
    mediaQuery.addEventListener('change', listener);
    return () => mediaQuery.removeEventListener('change', listener);
  }, []);

  useEffect(() => {
    document.documentElement.setAttribute('data-mantine-color-scheme', resolvedTheme);
  }, [resolvedTheme]);

  const value = useMemo<ThemePreferenceContextValue>(() => {
    const setPreference = (next: ThemePreference) => {
      try {
        localStorage.setItem(STORAGE_KEY, next);
      } catch {
        /* ignore persistence failures */
      }
      setPreferenceState(next);
    };

    return {
      preference,
      resolvedTheme,
      setPreference,
      toggle: () => setPreference(resolvedTheme === 'dark' ? 'light' : 'dark'),
    };
  }, [preference, resolvedTheme]);

  return (
    <ThemePreferenceContext.Provider value={value}>
      <MantineProvider defaultColorScheme="auto" forceColorScheme={resolvedTheme} theme={managementTheme} env={env}>
        <Notifications position="top-right" />
        {children}
      </MantineProvider>
    </ThemePreferenceContext.Provider>
  );
}
