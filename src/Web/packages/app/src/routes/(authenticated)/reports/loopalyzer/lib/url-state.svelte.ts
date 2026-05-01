/**
 * Reactive URL-state hook for the Loopalyzer report. Bind to this in components.
 * Pure helpers live in `./url-state.ts` for testability.
 */
import { useSearchParams } from "runed/kit";
import { LoopalyzerParamsSchema, type LoopalyzerParams } from "./url-state";

export type { LoopalyzerParams };
export { LoopalyzerParamsSchema };
export {
  type ViewMode,
  type TimeWindow,
  decodeTimeWindow,
  encodeTimeWindow,
  decodeEventTypes,
  encodeEventTypes,
} from "./url-state";

export function useLoopalyzerParams() {
  return useSearchParams(LoopalyzerParamsSchema, { showDefaults: true });
}
