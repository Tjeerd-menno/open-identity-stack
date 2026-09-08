# OpenIdentityStack Management Web

Operator console for OpenIdentityStack, rebuilt from the **OpenIdentityStack design
system** (Mantine foundation). It is the supported browser frontend for operator
workflows: Overview, Users, Roles, Groups, Permissions, Applications, Sessions, Identity
providers, Authentication settings and Audit.

> The previous implementation is preserved at `../OpenIdentityStack.ManagementWeb`.

## Tech stack

React 19 · Mantine 8 · TanStack Query · React Router 7 · Vite · oidc-client-ts. The Admin
API is consumed through the shared `@openidentitystack/admin-api-client` workspace package.

## Design

The UI follows the OpenIdentityStack design system: native system font, blue primary
(light shade 6 / dark shade 8), 8px radius, flat surfaces with 1px hairline borders, pill
badges, Lucide icons, a fixed 264px sidebar + 61px topbar and a 1080px content column.
Scheme-aware `--mw-*` tokens live in `src/styles.css`; the Mantine theme in `src/theme.ts`.

## Local development

```powershell
npm install
npm run dev
```

The Aspire AppHost configures `VITE_API_BASE_URL`, `VITE_OIDC_AUTHORITY`, and
`VITE_OIDC_CLIENT_ID` for local runs. The published container reads those same variables at
startup and writes them into `runtime-config.js`.

## Validation

Builds and type checks use TypeScript 7 through the `@typescript/native` npm alias.
The `typescript` dependency aliases Microsoft's `@typescript/typescript6` compatibility
package because typescript-eslint still needs the TypeScript 6 compiler API. This
development-only package exposes `tsc6`, leaving `tsc` to TypeScript 7 without a binary
name conflict. See Microsoft's [side-by-side migration guidance](https://devblogs.microsoft.com/typescript/announcing-typescript-7-0/#running-side-by-side-with-typescript-6.0).
Keep this compatibility alias until typescript-eslint supports the native compiler API.

Vitest and `@vitest/coverage-v8` must use the same exact version. Dependabot groups
them together for both major and minor/patch updates. Run coverage after upgrading
to verify the existing thresholds as well as the tests.

```powershell
npm run build
npm run lint
npm run type-check
npm test
npm run test:coverage
```


