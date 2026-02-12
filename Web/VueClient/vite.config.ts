/// <reference types="vitest" />

import { URL, fileURLToPath } from 'node:url'
import vue from '@vitejs/plugin-vue'
import analyzer from 'rollup-plugin-analyzer'
import { defineConfig } from 'vite'

// https://vitejs.dev/config/
export default defineConfig({
  resolve: {
    alias: {
      src: fileURLToPath(new URL('src', import.meta.url)),
    },
  },
  build: {
    // Output directly to the C# project's wwwroot for production serving
    outDir: '../../King-of-the-Garbage-Hill/wwwroot',
    emptyOutDir: true,
  },
  plugins: [
    vue(),
    analyzer({ summaryOnly: true }),
  ],
  server: {
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:3535',
        changeOrigin: true,
      },
      '/gamehub': {
        target: 'http://127.0.0.1:3535',
        ws: true,
        changeOrigin: true,
      },
      '/art': {
        target: 'http://127.0.0.1:3535',
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: 'happy-dom',
    setupFiles: './src/setupTests.ts',
    globals: true,
    snapshotFormat: {
      escapeString: false,
    },
    coverage: {
      enabled: true,
      provider: 'v8',
      include: [
        'src',
      ],
      exclude: [
        'src/*.{ts,vue}',
        'src/services/api.ts',
        'src/setupTests.ts',
        'src/utils/test',
        '**/*.d.ts',
      ],
      all: true,
    },
  },
})
