import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['app_icon.png'],
      manifest: {
        name: 'MemoBud',
        short_name: 'MemoBud',
        description: 'MemoBud delivers high-precision productivity workspaces.',
        theme_color: '#004f96',
        background_color: '#ffffff',
        display: 'standalone',
        icons: [
          {
            src: 'app_icon.png',
            sizes: '192x192 512x512',
            type: 'image/png',
            purpose: 'any maskable'
          }
        ]
      }
    })
  ],
})
