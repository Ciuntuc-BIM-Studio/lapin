import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { resolve } from 'node:path';

// The engine's wire protocol and client come from the BubbleBIM Pro engine
// itself (vendor/bubblebim-pro), so Lapin always speaks the engine's language.
export default defineConfig({
  plugins: [react()],
  resolve: { alias: { '@bbim/platform': resolve(__dirname, '../vendor/bubblebim-pro/ui/src/platform') } },
  clearScreen: false,
  server: { port: 5180, strictPort: true, fs: { allow: [resolve(__dirname, '..')] } },
  build: { outDir: 'dist', emptyOutDir: true, chunkSizeWarningLimit: 1500 },
});
