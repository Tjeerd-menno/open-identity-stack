import react from '@vitejs/plugin-react';
import path from 'node:path';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [react()],
  // Bake process.env.VITE_E2E_TEST_MODE into the bundle as a plain global constant.
  // Using a non-import.meta.env identifier avoids Vite's own env-inlining pass
  // (which only reads .env files and would silently shadow a define on import.meta.env.*).
  define: {
    __E2E_TEST_MODE__: JSON.stringify(process.env.VITE_E2E_TEST_MODE === 'true'),
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: Number.parseInt(process.env.PORT || '5176', 10),
    strictPort: true,
    proxy: {
      '/api': {
        target: process.env.VITE_API_BASE_URL || 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
});
