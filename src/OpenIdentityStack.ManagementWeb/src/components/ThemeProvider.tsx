/* eslint-disable react-refresh/only-export-components */
import {
  Alert,
  Badge,
  Button,
  MantineProvider,
  NavLink,
  Paper,
  Table,
  TextInput,
  createTheme,
} from '@mantine/core';
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

const managementTheme = createTheme({
  primaryColor: 'blue',
  primaryShade: { light: 6, dark: 5 },
  autoContrast: true,
  luminanceThreshold: 0.35,
  fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
  headings: {
    fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
    fontWeight: '650',
    sizes: {
      h1: { fontSize: '1.55rem', lineHeight: '1.25' },
      h2: { fontSize: '1.2rem', lineHeight: '1.3' },
      h3: { fontSize: '1rem', lineHeight: '1.35' },
    },
  },
  defaultRadius: 'sm',
  radius: {
    xs: '4px',
    sm: '6px',
    md: '8px',
    lg: '10px',
    xl: '12px',
  },
  spacing: {
    xs: '0.45rem',
    sm: '0.65rem',
    md: '0.9rem',
    lg: '1.2rem',
    xl: '1.6rem',
  },
  components: {
    Alert: Alert.extend({
      defaultProps: {
        radius: 'sm',
        variant: 'light',
      },
    }),
    Badge: Badge.extend({
      defaultProps: {
        radius: 'sm',
      },
    }),
    Button: Button.extend({
      defaultProps: {
        radius: 'sm',
      },
      styles: {
        section: {
          '& svg': {
            display: 'block',
          },
        },
      },
    }),
    NavLink: NavLink.extend({
      defaultProps: {
        variant: 'light',
      },
    }),
    Paper: Paper.extend({
      defaultProps: {
        radius: 'sm',
      },
    }),
    Table: Table.extend({
      defaultProps: {
        horizontalSpacing: 'sm',
        verticalSpacing: 'xs',
      },
    }),
    TextInput: TextInput.extend({
      defaultProps: {
        radius: 'sm',
      },
    }),
  },
  focusRing: 'auto',
});

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
      <MantineProvider defaultColorScheme="auto" forceColorScheme={resolvedTheme} theme={managementTheme}>
        <Notifications position="top-right" />
        {children}
      </MantineProvider>
    </ThemePreferenceContext.Provider>
  );
}
