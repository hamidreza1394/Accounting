import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { fileURLToPath } from 'node:url';

const vendorServerUrl = process.env.FARANO_VENDOR_SERVER_URL ?? 'https://localhost:5001';

export default defineConfig({
  base: '/',
  plugins: [react(), tailwindcss()],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  build: { outDir: '../Vendor.Host/wwwroot', emptyOutDir: true, sourcemap: false },
  server: { port: 5174, strictPort: true, proxy: { '/api': { target: vendorServerUrl, changeOrigin: false, secure: false }, '/health': { target: vendorServerUrl, changeOrigin: false, secure: false } } },
  test: { environment: 'jsdom', globals: true, setupFiles: ['./src/test/setup.ts'], css: false },
});
