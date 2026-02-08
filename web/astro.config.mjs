import { defineConfig } from "astro/config";

export default defineConfig({
  // Static site generation - produces a single HTML file
  output: "static",
  build: {
    // Inline all CSS into <style> tags (no external .css files)
    inlineStylesheets: "always",
    // Build output goes to dist/
    format: "file",
  },
  vite: {
    build: {
      // Inline all JS and CSS so the report is a single self-contained file
      assetsInlineLimit: 1_000_000,
      rollupOptions: {
        output: {
          // Force everything into a single chunk
          manualChunks: undefined,
        },
      },
    },
  },
});
