import { defaultPaletteFor, sourcePalette } from './palette-defaults';
import { paletteKey } from './scenes';
import type { AssetKey } from './assets';

/**
 * Bundled assets laid out as `assets/<artwork>/<paletteKey>/<file>`. Vite
 * rewrites each entry to a hashed URL at build time; nothing is fetched until
 * a loader is called.
 *
 * The map's keys alone are a third of the library's static weight, so this
 * module is only ever imported dynamically, by `assets.ts`, on the first
 * resolve that may draw a baked or static asset.
 */
const bundled = import.meta.glob('../../assets/*/*/*.{webp,json}', {
  query: '?url',
  import: 'default',
}) as Record<string, () => Promise<string>>;

function bundledPath(key: AssetKey, file: string): string {
  const exact = `../../assets/${key.id}/${paletteKey(sourcePalette(key.id, key.palette), key.surface)}/${file}`;
  if (exact in bundled) return exact;
  const fallback = `../../assets/${key.id}/${paletteKey(defaultPaletteFor(key.id), key.surface)}/${file}`;
  return fallback in bundled ? fallback : exact;
}

export function hasBundled(key: AssetKey, file: string): boolean {
  return bundledPath(key, file) in bundled;
}

export function bundledUrl(key: AssetKey, file: string): Promise<string> | undefined {
  return bundled[bundledPath(key, file)]?.();
}

export function bundledIds(): string[] {
  const ids = new Set<string>();
  for (const path of Object.keys(bundled)) {
    const match = /^\.\.\/\.\.\/assets\/([^/]+)\//.exec(path);
    if (match) ids.add(match[1]!);
  }
  return Array.from(ids).sort();
}
