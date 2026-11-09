import react from '@vitejs/plugin-react'
import { defineConfig, type ProxyOptions } from 'vite'

const apiProxy: Record<string, ProxyOptions> = {
  '/api': {
    target: process.env.HELPDESK_API_URL ?? 'http://127.0.0.1:58080',
    changeOrigin: true,
    rewrite: (path) => path.replace(/^\/api/, ''),
  },
}

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: apiProxy,
  },
  preview: {
    proxy: apiProxy,
  },
})
