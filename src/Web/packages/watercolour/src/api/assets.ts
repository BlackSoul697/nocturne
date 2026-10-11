import type { ArtworkId, PaletteId, Surface } from '../types';
import { type BakedManifest, parseBakedManifest } from './baked';
import { WatercolourError } from './errors';
import { iconAssetId, sourcePalette } from './palette-defaults';
import { paletteKey } from './scenes';

export { DEFAULT_PALETTE, defaultPaletteFor } from './palette-defaults';

export type AssetVariant = 'final' | 'final-small' | 'strip' | 'manifest';

/**
 * WebP, not PNG. The artwork is soft alpha washes, which PNG stores badly: the
 * whole catalogue is 4.4 MB rather than 20 MB, and the eight files one
 * paint-drop surface can draw are 76 KB rather than 260 KB.
 *
 * `scripts/to-webp.mjs` encodes them after the bake, keeping the alpha plane
 * lossless because alpha is what carries the shape.
 */
const FILE_NAMES: Record<AssetVariant, string> = {
  final: 'final-512.webp',
  'final-small': 'final-128.webp',
  strip: 'strip.webp',
  manifest: 'strip.json',
};

/** The bundled asset key for a baked Lucide icon: `assets/lucide-<name>/...`. */
export function iconAssetKey(name: string, palette?: PaletteId, surface?: Surface): AssetKey {
  return { id: iconAssetId(name), palette, surface };
}

export interface AssetOptions {
  /** Serve assets from `<assetBaseUrl>/<artwork>/<paletteKey>/<file>` instead of the bundle. */
  assetBaseUrl?: string;
  /** Explicit URLs override both the bundle and `assetBaseUrl`. */
  assets?: Partial<Record<AssetVariant, string>>;
}

export interface AssetKey {
  id: ArtworkId | string;
  palette?: PaletteId;
  surface?: Surface;
}

let bundle: Promise<typeof import('./bundled-assets')> | undefined;

/** The bundled-asset URL map; see `bundled-assets.ts` for why it loads on demand. */
function loadBundle(): Promise<typeof import('./bundled-assets')> {
  bundle ??= import('./bundled-assets');
  return bundle;
}

export async function hasBundledAsset(key: AssetKey, variant: AssetVariant): Promise<boolean> {
  return (await loadBundle()).hasBundled(key, FILE_NAMES[variant]);
}

/**
 * Whether a fallback of this kind can be attempted at all; a base URL is
 * trusted without probing. Only a bundled asset loads the URL map to answer.
 */
export async function assetAvailable(key: AssetKey, variant: AssetVariant, options: AssetOptions = {}): Promise<boolean> {
  if (options.assets?.[variant]) return true;
  if (options.assetBaseUrl) return true;
  return hasBundledAsset(key, variant);
}

export async function assetUrl(key: AssetKey, variant: AssetVariant, options: AssetOptions = {}): Promise<string | undefined> {
  const explicit = options.assets?.[variant];
  if (explicit) return explicit;
  if (options.assetBaseUrl) {
    const base = options.assetBaseUrl.replace(/\/+$/, '');
    return `${base}/${key.id}/${paletteKey(sourcePalette(key.id, key.palette), key.surface)}/${FILE_NAMES[variant]}`;
  }
  return (await loadBundle()).bundledUrl(key, FILE_NAMES[variant]);
}

export async function loadManifest(url: string): Promise<BakedManifest> {
  const response = await fetch(url);
  if (!response.ok) throw new WatercolourError('AssetMissing', `${url}: HTTP ${response.status}`);
  return parseBakedManifest(await response.json());
}

/** Artworks with at least one bundled asset set, for galleries and tests. */
export async function bundledArtworkIds(): Promise<string[]> {
  return (await loadBundle()).bundledIds();
}
