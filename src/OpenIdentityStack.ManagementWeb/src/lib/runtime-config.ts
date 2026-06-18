type RuntimeConfig = {
  apiBaseUrl?: string;
  oidcAuthority?: string;
  oidcClientId?: string;
};

function readWindowRuntimeConfig(): RuntimeConfig {
  if (typeof window === 'undefined') {
    return {};
  }

  return window.__OIS_RUNTIME_CONFIG__ ?? {};
}

function getBrowserOrigin(): string {
  if (typeof window === 'undefined') {
    return 'http://localhost:5000';
  }

  return window.location.origin;
}

function getDefaultAuthority(): string {
  if (import.meta.env.DEV || import.meta.env.MODE === 'test') {
    return 'http://localhost:5000';
  }

  return getBrowserOrigin();
}

function firstNonEmpty(...values: Array<string | undefined>): string | undefined {
  return values.find((value) => typeof value === 'string' && value.length > 0);
}

export function getApiBaseUrl(): string {
  return firstNonEmpty(
    readWindowRuntimeConfig().apiBaseUrl,
    import.meta.env.VITE_API_BASE_URL
  ) ?? getDefaultAuthority();
}

export function getOidcAuthority(): string {
  return firstNonEmpty(
    readWindowRuntimeConfig().oidcAuthority,
    import.meta.env.VITE_OIDC_AUTHORITY
  ) ?? getDefaultAuthority();
}

export function getOidcClientId(): string {
  return firstNonEmpty(
    readWindowRuntimeConfig().oidcClientId,
    import.meta.env.VITE_OIDC_CLIENT_ID
  ) ?? 'management-web-client';
}
