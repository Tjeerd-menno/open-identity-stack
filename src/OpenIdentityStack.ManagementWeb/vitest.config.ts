import react from '@vitejs/plugin-react';
import path from 'node:path';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [react()],
  define: {
    __E2E_TEST_MODE__: 'false',
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    reporters: ['dot'],
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
      '@openidentitystack/admin-api-client': path.resolve(
        __dirname,
        '../frontend-packages/admin-api-client/src/index.ts'
      ),
    },
  },
});
