/* eslint-disable react-refresh/only-export-components */
import {
  Alert,
  Button,
  MantineProvider,
  NavLink,
  Table,
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

// Aligned with the OpenIdentityStack Design System. Spacing (10/12/16/20/32) and
// the radius scale (2/4/8/16/32) are the Mantine v8 defaults the design tokens
// encode, so they are left unset. We keep the Inter webfont as the brand typeface
// and override the defaults the design calls out: control radius `md`, dark primary
// shade 8, heading weight 700, and a heading scale sized to the product screens.
const managementTheme = createTheme({
  primaryColor: 'blue',
  primaryShade: { light: 6, dark: 8 },
  autoContrast: true,
  luminanceThreshold: 0.35,
  fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
  headings: {
    fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
    fontWeight: '700',
    // Sized to the design system's Management Web screens (page titles 26px,
    // section headings 16px), not the larger generic foundation specimen.
    sizes: {
      h1: { fontSize: '1.625rem', lineHeight: '1.3' },
      h2: { fontSize: '1.25rem', lineHeight: '1.35' },
      h3: { fontSize: '1rem', lineHeight: '1.4' },
    },
  },
  defaultRadius: 'md',
  components: {
    Alert: Alert.extend({
      defaultProps: {
        variant: 'light',
      },
    }),
    Button: Button.extend({
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
    Table: Table.extend({
      defaultProps: {
        horizontalSpacing: 'sm',
        verticalSpacing: 'xs',
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
