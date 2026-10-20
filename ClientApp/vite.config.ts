import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// The client is served by the ASP.NET Core server under /app (same origin as the API and
// the Razor sign-in page), so the authentication cookie and the anti-forgery cookie work
// without CORS. In development Vite proxies the server routes to the ASP.NET Core host.
const serverUrl = process.env.FARANOU_SERVER_URL ?? 'https://localhost:5001';

const proxied = ['/api', '/Account', '/fonts', '/images', '/css', '/lib', '/favicon.ico'];

export default defineConfig({
  base: '/app/',
  plugins: [react(), tailwindcss()],
  build: {
    outDir: '../Server/wwwroot/app',
    emptyOutDir: true,
    sourcemap: false,
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: Object.fromEntries(
      proxied.map((path) => [path, { target: serverUrl, changeOrigin: false, secure: false }]),
    ),
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
});
