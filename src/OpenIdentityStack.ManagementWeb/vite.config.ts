import react from '@vitejs/plugin-react';
import path from 'node:path';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [react()],
  // Explicitly bake process.env.VITE_* values set by the orchestrator (e.g. Aspire)
  // into the client bundle. Vite's loadEnv() only reads from .env files, not from
  // the shell environment, so runtime-injected VITE_* vars need this define bridge.
  define: {
    'import.meta.env.VITE_E2E_TEST_MODE': JSON.stringify(process.env.VITE_E2E_TEST_MODE ?? ''),
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
