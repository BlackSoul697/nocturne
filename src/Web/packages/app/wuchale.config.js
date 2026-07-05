// @ts-check
import { adapter as svelte } from "@wuchale/svelte"
import { adapter as js } from 'wuchale/adapter-vanilla'
import { defineConfig, gemini, pofile } from "wuchale"
import supportedLocales from "../../supportedLocales.json" with { type: 'json' };

// Both adapters share one catalog set (same storage key → shared .po files).
const storage = pofile({ location: '../../locales/{locale}.po' })

export default defineConfig({
    locales: supportedLocales,
    localesDir: '../../locales',
    adapters: {
        // Both packages' files are listed in both configs so a single
        // extraction run (from either package) produces the complete shared
        // catalog. An extraction that sees only one package obsoletes the
        // other package's messages.
        main: svelte({
            loader: 'sveltekit',
            sourceLocale: 'en',
            storage,
            files: [
                'src/**/*.svelte',
                'src/**/*.svelte.{js,ts}',

                '../portal/src/**/*.svelte',
                '../portal/src/**/*.svelte.{js,ts}',
            ],
        }),
        js: js({
            loader: 'vite',
            sourceLocale: 'en',
            storage,
            files: [
                'src/**/+{page,layout}.{js,ts}',
                'src/**/+{page,layout}.server.{js,ts}',

                '../portal/src/**/+{page,layout}.{js,ts}',
                '../portal/src/**/+{page,layout}.server.{js,ts}',
            ],
        })
    },
    ai: gemini({
        model: 'gemini-3-flash-preview',
        batchSize: 40,
        parallel: 5,
        think: true, // default: false
  }),
})
