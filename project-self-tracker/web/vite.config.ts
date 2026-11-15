import path from "node:path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

// Dev: Vite proxies /api and /health to the API on :5080.
// Build: output goes to the API's wwwroot so one process serves everything (local "prod").
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { "@": path.resolve(import.meta.dirname, "./src") } },
  server: {
    host: "localhost",
    port: 5173,
    strictPort: true,
    proxy: {
      "/api": { target: "http://localhost:5080", changeOrigin: false },
      "/health": { target: "http://localhost:5080", changeOrigin: false },
      "/openapi": { target: "http://localhost:5080", changeOrigin: false },
    },
  },
  build: {
    outDir: "../src/SelfTracker.Api/wwwroot",
    emptyOutDir: true,
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    css: false,
  },
});
