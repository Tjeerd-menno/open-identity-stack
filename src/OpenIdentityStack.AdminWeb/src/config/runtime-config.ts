type RuntimeConfigKey =
  | 'VITE_API_BASE_URL'
  | 'VITE_OIDC_AUTHORITY'
  | 'VITE_OIDC_CLIENT_ID'
  | 'VITE_OIDC_REDIRECT_URI'
  | 'VITE_OIDC_POST_LOGOUT_REDIRECT_URI'
  | 'VITE_OIDC_SILENT_REDIRECT_URI'
  | 'VITE_OIDC_SCOPE'
  | 'VITE_APP_NAME'
  | 'VITE_APP_VERSION';

type RuntimeConfigMap = Partial<Record<RuntimeConfigKey, string>>;

declare global {
  interface Window {
    __OPENID_MODULE_CONFIG__?: RuntimeConfigMap;
  }
}

const buildTimeConfig = import.meta.env as Record<string, string | undefined>;

export function getRuntimeConfig(key: RuntimeConfigKey, fallback = ''): string {
  return window.__OPENID_MODULE_CONFIG__?.[key] ?? buildTimeConfig[key] ?? fallback;
}

export {};