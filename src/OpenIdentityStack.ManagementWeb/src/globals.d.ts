// Global constant injected by vite.config.ts define block. Set to true when
// VITE_E2E_TEST_MODE=true is in the Vite process environment (Aspire test mode).
declare const __E2E_TEST_MODE__: boolean;

interface Window {
  __OIS_E2E_AUTH__?: boolean;
  __OIS_RUNTIME_CONFIG__?: {
    apiBaseUrl?: string;
    oidcAuthority?: string;
    oidcClientId?: string;
  };
}
