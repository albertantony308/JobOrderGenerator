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
        name: 'Service Memo Manager',
        short_name: 'Service Memo Manager',
        description: 'Service Memo Manager staff portal for tracking and updating job orders in real-time.',
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
