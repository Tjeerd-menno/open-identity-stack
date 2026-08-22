import react from '@vitejs/plugin-react';
import path from 'node:path';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [react()],
  test: {
    coverage: {
      provider: 'v8',
      reporter: ['lcov', 'text'],
      // Without an explicit include, v8 counts only files some test imported, so a module with
      // no tests at all is invisible to the thresholds below and adding one can raise the
      // percentages. Enumerating the sources makes untested code count against the gate.
      include: ['src/**/*.{ts,tsx}'],
      exclude: [
        'src/**/*.test.{ts,tsx}',
        'src/test/**',
        'src/globals.d.ts',
        // Bootstraps the app; nothing to assert and it is excluded from Sonar coverage too.
        'src/main.tsx',
      ],
      // Thresholds sit just under the current numbers so the gate blocks regressions. Raise
      // them as coverage improves; never lower them to make a red build go green.
      // Measured across every source file, not just the ones a test happens to import, so
      // these are much lower than the previously reported figures — that number was computed
      // against roughly half the codebase.
      thresholds: {
        statements: 22,
        branches: 24,
        functions: 17,
        lines: 22,
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
