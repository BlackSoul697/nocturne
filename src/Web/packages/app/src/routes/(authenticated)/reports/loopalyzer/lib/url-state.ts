/**
 * Pure helpers for Loopalyzer URL-state encoding. Lives in a `.ts` file (not
 * `.svelte.ts`) so unit tests can import without dragging in SvelteKit runtime
 * (`$app`).
 */
import { z } from "zod";

export type ViewMode = "average" | "spaghetti";

export const LoopalyzerParamsSchema = z.object({
  viewMode: z.enum(["average", "spaghetti"]).nullable().default("average"),
  timeShift: z.coerce.number().nullable().default(0), // 0 | 1
  /** Window-of-interest for anchor-meal selection. Encoded "HH:MM-HH:MM". */
  tsWindow: z.string().nullable().default("06:00-20:00"),
  /** Minimum carbs (g) for a treatment to qualify as an anchor meal. */
  tsMinCarbs: z.coerce.number().nullable().default(10),
  /** Comma-separated list of treatment event types eligible as anchor meals. Empty = all. */
  tsEventTypes: z.string().nullable().default(""),
  predictions: z.coerce.number().nullable().default(1),
  apsBands: z.coerce.number().nullable().default(0),
  profilesTable: z.coerce.number().nullable().default(0),
});

export type LoopalyzerParams = z.infer<typeof LoopalyzerParamsSchema>;

/** Decoded "HH:MM-HH:MM" → minute offsets from midnight (0..1440). */
export interface TimeWindow {
  startMin: number;
  endMin: number;
}

export function decodeTimeWindow(encoded: string | null | undefined): TimeWindow {
  const fallback: TimeWindow = { startMin: 6 * 60, endMin: 20 * 60 };
  if (!encoded) return fallback;
  const match = /^(\d{1,2}):(\d{2})-(\d{1,2}):(\d{2})$/.exec(encoded);
  if (!match) return fallback;
  const [, sh, sm, eh, em] = match;
  const startMin = clampMinute(Number(sh) * 60 + Number(sm));
  const endMin = clampMinute(Number(eh) * 60 + Number(em));
  if (endMin <= startMin) return fallback;
  return { startMin, endMin };
}

export function encodeTimeWindow(window: TimeWindow): string {
  return `${pad(Math.floor(window.startMin / 60))}:${pad(window.startMin % 60)}-${pad(Math.floor(window.endMin / 60))}:${pad(window.endMin % 60)}`;
}

export function decodeEventTypes(encoded: string | null | undefined): string[] {
  if (!encoded) return [];
  return encoded
    .split(",")
    .map((s) => s.trim())
    .filter((s) => s.length > 0);
}

export function encodeEventTypes(types: readonly string[]): string {
  return types.filter((t) => t.length > 0).join(",");
}

function clampMinute(m: number): number {
  if (!Number.isFinite(m)) return 0;
  return Math.min(Math.max(0, Math.round(m)), 1440);
}

function pad(n: number): string {
  return n.toString().padStart(2, "0");
}
