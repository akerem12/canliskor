import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The dev server proxies API and hub calls to the backend, so the browser sees a single origin:
// no CORS setup, and the same relative URLs work when the backend serves the built app.
const backend = 'http://localhost:5272'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': backend,
      '/hubs': { target: backend, ws: true },
    },
  },
})
