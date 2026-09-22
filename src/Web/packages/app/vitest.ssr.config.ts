import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";
import { svelte } from "@sveltejs/vite-plugin-svelte";
import { wuchale } from "wuchale/vite";

/**
 * Server rendering, with the translation transform in the pipeline.
 *
 * The other suites compile the app or call its components directly; neither
 * renders a page through the transform wuchale applies. A transform can leave
 * the build green and still change what runs at render time, which is how the
 * sidebar provider's setContext stopped running on the server. These tests
 * render, so they see it.
 */
export default defineConfig({
  plugins: [svelte(), wuchale()],
  test: {
    include: ["src/**/*.ssr.test.ts"],
    environment: "node",
    alias: {
      $lib: fileURLToPath(new URL("./src/lib", import.meta.url)),
      "$app/environment": fileURLToPath(
        new URL("./src/lib/test-stubs/app-environment-node.ts", import.meta.url)
      ),
      "$app/navigation": fileURLToPath(
        new URL("./src/lib/test-stubs/app-navigation.ts", import.meta.url)
      ),
      "$app/state": fileURLToPath(
        new URL("./src/lib/test-stubs/app-state.ts", import.meta.url)
      ),
    },
  },
});
