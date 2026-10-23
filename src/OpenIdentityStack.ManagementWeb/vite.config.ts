import react from '@vitejs/plugin-react';
import path from 'node:path';
import { defineConfig } from 'vite';

// Vite 8 removed the object form of output.manualChunks. Rolldown groups are
// the supported split, and the package names match the installed dependencies
// (react-router, not react-router-dom). Higher priority keeps React out of the
// Mantine group when dependencies are captured recursively.
const vendorChunkGroups = [
  {
    name: 'react',
    test: /node_modules[\\/](react-dom|react-router|react)[\\/]/,
    priority: 30,
  },
  {
    name: 'mantine',
    test: /node_modules[\\/]@mantine[\\/](core|hooks|form|notifications)[\\/]/,
    priority: 20,
  },
  {
    name: 'query',
    // QueryClient and the caches are implemented in @tanstack/query-core.
    // Matching only react-query leaves those modules in the app entry.
    test: /node_modules[\\/]@tanstack[\\/](?:react-query|query-core)[\\/]/,
    priority: 20,
  },
  {
    name: 'oidc',
    test: /node_modules[\\/]oidc-client-ts[\\/]/,
    priority: 20,
  },
];

export default defineConfig(({ mode }) => {
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
    build: {
      sourcemap: false,
      target: 'es2022',
      rolldownOptions: {
        output: {
          // An app build resolves minify to `true`. A partial object replaces that
          // boolean, so mangling stays explicit while console and debugger drop.
          minify:
            mode === 'production'
              ? {
                  compress: {
                    dropConsole: true,
                    dropDebugger: true,
                  },
                  mangle: true,
                }
              : undefined,
          codeSplitting: {
            groups: vendorChunkGroups,
          },
        },
      },
    },
  };
});
