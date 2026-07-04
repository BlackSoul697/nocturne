import adapter from "@sveltejs/adapter-static";
import { vitePreprocess } from "@sveltejs/vite-plugin-svelte";

/** @type {import('@sveltejs/kit').Config} */
const config = {
  preprocess: vitePreprocess(),
  kit: {
    // Tauri serves the built frontend from disk; SPA fallback, no SSR.
    adapter: adapter({ fallback: "index.html" }),
  },
};

export default config;
