import react from '@vitejs/plugin-react';
import path from 'node:path';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [react()],
  test: {
    coverage: {
      provider: 'v8',
      reporter: ['lcov', 'text'],
      // Thresholds sit just under the current numbers so the gate blocks regressions.
      // CI collected coverage before this without checking it against anything. Raise these
      // as coverage improves; never lower them to make a red build go green.
      thresholds: {
        statements: 55,
        branches: 54,
        functions: 42,
        lines: 56,
      },
    },
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    reporters: ['dot'],
    testTimeout: 20000,
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
