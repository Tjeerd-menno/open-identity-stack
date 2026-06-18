import { afterEach, describe, expect, it } from 'vitest';
import { getApiBaseUrl, getOidcAuthority, getOidcClientId } from './runtime-config';

const originalRuntimeConfig = window.__OIS_RUNTIME_CONFIG__;

afterEach(() => {
  window.__OIS_RUNTIME_CONFIG__ = originalRuntimeConfig;
});

describe('runtime config', () => {
  it('prefers runtime-injected values over build-time defaults', () => {
    window.__OIS_RUNTIME_CONFIG__ = {
      apiBaseUrl: 'https://api.example.com',
      oidcAuthority: 'https://identity.example.com',
      oidcClientId: 'ops-web',
    };

    expect(getApiBaseUrl()).toBe('https://api.example.com');
    expect(getOidcAuthority()).toBe('https://identity.example.com');
    expect(getOidcClientId()).toBe('ops-web');
  });

  it('falls back to the current browser origin when runtime values are absent', () => {
    window.__OIS_RUNTIME_CONFIG__ = {};

    expect(getApiBaseUrl()).toBe(window.location.origin);
    expect(getOidcAuthority()).toBe(window.location.origin);
    expect(getOidcClientId()).toBe('management-web-client');
  });
});
