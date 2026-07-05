import type { Handle } from '@sveltejs/kit';
import { runWithLocale, loadLocales } from 'wuchale/load-utils/server';
import * as main from '../../../locales/main.loader.server.svelte.js';
import * as js from '../../../locales/js.loader.server.js';
import { locales } from '../../../locales/data.js';

// The portal is fully prerendered, so this hook only runs at build time.
// Without loaded catalogs the wuchale runtime cannot resolve any message
// during prerender. Pin the source locale for deterministic prerendered
// HTML; the client applies the visitor's locale at runtime (+layout.ts).
// Await so no page can prerender before catalogs are registered: a lookup
// against an unloaded runtime silently renders every message as ''.
await loadLocales(main.key, main.loadCount, main.loadCatalog, locales);
await loadLocales(js.key, js.loadCount, js.loadCatalog, locales);

export const handle: Handle = async ({ event, resolve }) =>
	await runWithLocale('en', () => resolve(event));
