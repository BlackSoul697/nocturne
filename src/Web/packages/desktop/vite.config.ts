import { sveltekit } from "@sveltejs/kit/vite";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [tailwindcss(), sveltekit()],
  // Tauri expects a fixed dev port (tauri.conf.json devUrl).
  server: {
    port: 1420,
    strictPort: true,
  },
  clearScreen: false,
});
