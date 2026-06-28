import { Badge, Button, Card, NavLink, Paper, Table, TextInput, createTheme } from '@mantine/core';

// Mantine's default native system font stack — zero webfont weight, exactly what the
// OpenIdentityStack design system specifies. Mono is reserved for IDs / scopes / grants.
const SYSTEM_FONT_STACK =
  '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif, "Apple Color Emoji", "Segoe UI Emoji"';
const MONO_FONT_STACK =
  'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace';

/**
 * Mantine theme matching the OpenIdentityStack design tokens: blue primary
 * (light shade 6 / dark shade 8), 8px default radius, native system font,
 * 700 headings, flat surfaces, pill badges.
 */
export const managementTheme = createTheme({
  primaryColor: 'blue',
  primaryShade: { light: 6, dark: 8 },
  autoContrast: true,
  luminanceThreshold: 0.35,
  fontFamily: SYSTEM_FONT_STACK,
  fontFamilyMonospace: MONO_FONT_STACK,
  headings: {
    fontFamily: SYSTEM_FONT_STACK,
    fontWeight: '700',
    sizes: {
      h1: { fontSize: '1.625rem', lineHeight: '1.3' }, // 26px — page titles
      h2: { fontSize: '1.375rem', lineHeight: '1.35' }, // 22px
      h3: { fontSize: '1.125rem', lineHeight: '1.4' }, // 18px
      h4: { fontSize: '1rem', lineHeight: '1.45' }, // 16px
    },
  },
  defaultRadius: 'md',
  radius: { xs: '0.125rem', sm: '0.25rem', md: '0.5rem', lg: '1rem', xl: '2rem' },
  spacing: { xs: '0.625rem', sm: '0.75rem', md: '1rem', lg: '1.25rem', xl: '2rem' },
  shadows: {
    xs: '0 1px 3px rgba(0, 0, 0, 0.05), 0 1px 2px rgba(0, 0, 0, 0.1)',
    sm: '0 1px 3px rgba(0, 0, 0, 0.05), rgba(0, 0, 0, 0.05) 0 10px 15px -5px, rgba(0, 0, 0, 0.04) 0 7px 7px -5px',
    md: '0 1px 3px rgba(0, 0, 0, 0.05), rgba(0, 0, 0, 0.05) 0 20px 25px -5px, rgba(0, 0, 0, 0.04) 0 10px 10px -5px',
    lg: '0 1px 3px rgba(0, 0, 0, 0.05), rgba(0, 0, 0, 0.05) 0 28px 23px -7px, rgba(0, 0, 0, 0.04) 0 12px 12px -7px',
    xl: '0 1px 3px rgba(0, 0, 0, 0.05), rgba(0, 0, 0, 0.05) 0 36px 28px -7px, rgba(0, 0, 0, 0.04) 0 17px 17px -7px',
  },
  components: {
    Badge: Badge.extend({ defaultProps: { radius: 'xl' } }),
    Button: Button.extend({
      styles: { section: { '& svg': { display: 'block' } } },
    }),
    Card: Card.extend({ defaultProps: { radius: 'md', withBorder: true, shadow: undefined } }),
    NavLink: NavLink.extend({ defaultProps: { variant: 'light' } }),
    Paper: Paper.extend({ defaultProps: { radius: 'md' } }),
    Table: Table.extend({ defaultProps: { horizontalSpacing: 'lg', verticalSpacing: 'sm' } }),
    TextInput: TextInput.extend({ defaultProps: { radius: 'md' } }),
  },
  focusRing: 'auto',
});
