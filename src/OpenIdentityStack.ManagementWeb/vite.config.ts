import react from '@vitejs/plugin-react';
import path from 'node:path';
import { defineConfig } from 'vite';

export default defineConfig(() => {
  return {
    plugins: [react()],
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
      port: Number.parseInt(process.env['PORT'] || '5175', 10),
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
