import '@testing-library/jest-dom/vitest';
import { afterEach, vi } from 'vitest';
import { cleanup } from '@testing-library/react';

const getComputedStyle = window.getComputedStyle;
window.getComputedStyle = (element) => getComputedStyle(element);
window.HTMLElement.prototype.scrollIntoView = () => {};

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: vi.fn().mockImplementation((query: string) => ({
    matches: query.includes('dark'),
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })),
});

Object.defineProperty(document, 'fonts', {
  value: { addEventListener: vi.fn(), removeEventListener: vi.fn() },
});

class ResizeObserverMock {
  observe() {}
  unobserve() {}
  disconnect() {}
}

globalThis.ResizeObserver = ResizeObserverMock;
window.__OIS_RUNTIME_CONFIG__ = {
  apiBaseUrl: 'http://localhost:5000',
  oidcAuthority: 'http://localhost:5000',
  oidcClientId: 'management-web-client',
};

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  localStorage.clear();
  window.__OIS_RUNTIME_CONFIG__ = {
    apiBaseUrl: 'http://localhost:5000',
    oidcAuthority: 'http://localhost:5000',
    oidcClientId: 'management-web-client',
  };
});
