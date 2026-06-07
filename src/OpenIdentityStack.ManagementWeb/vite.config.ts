import react from '@vitejs/plugin-react';
import path from 'node:path';
import { defineConfig, loadEnv } from 'vite';

export default defineConfig(({ command, mode }) => {
  // loadEnv reads .env, .env.local, .env.[mode] files in addition to process.env.
  // Using prefix '' captures ALL vars (not just VITE_*) so we can read VITE_E2E_TEST_MODE
  // that may have been written to .env.local by the test fixture.
  const env = loadEnv(mode, process.cwd(), '');
  const isE2ETestMode =
    (env['VITE_E2E_TEST_MODE'] ?? process.env['VITE_E2E_TEST_MODE']) === 'true';

  return {
    plugins: isE2ETestMode && command === 'serve' ? [] : [react()],
    // Bake the E2E flag into the client bundle so auth.tsx can select MockAuthProvider.
    define: {
      __E2E_TEST_MODE__: JSON.stringify(isE2ETestMode),
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
    server: {
      port: Number.parseInt(process.env['PORT'] || '5176', 10),
      strictPort: true,
      proxy: {
        '/api': {
          target: process.env['VITE_API_BASE_URL'] || 'http://localhost:5000',
          changeOrigin: true,
        },
      },
    },
  };
});
