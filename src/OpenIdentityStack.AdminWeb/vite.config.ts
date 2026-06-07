import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
      "@openidentitystack/admin-api-client": path.resolve(
        __dirname,
        "../frontend-packages/admin-api-client/src/index.ts"
      ),
      "@openidentitystack/permission-semantics": path.resolve(
        __dirname,
        "../frontend-packages/permission-semantics/src/index.ts"
      ),
    },
  },
  server: {
    port: Number.parseInt(process.env.PORT || '5173', 10),
    strictPort: true,
    proxy: {
      '/api': {
        target: process.env.VITE_API_BASE_URL || 'http://localhost:5000',
        changeOrigin: true,
      }
    }
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks: (id): string | undefined => {
          if (id.includes('/node_modules/react/') || id.includes('/node_modules/react-dom/')) {
            return 'vendor';
          }
          if (id.includes('/node_modules/react-router-dom/')) {
            return 'router';
          }
          if (id.includes('/node_modules/@tanstack/react-query/')) {
            return 'query';
          }
          return undefined;
        },
      },
    },
    minify: 'oxc',
    sourcemap: false,
    target: 'es2020'
  }
})
