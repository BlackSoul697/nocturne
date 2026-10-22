import { untrack } from "svelte";
import type { LoopAnalysisResponse } from "$lib/api";
import { contextResource } from "$lib/hooks/resource-context.svelte";

type Loaded<R> = { loading: boolean; error: unknown; current: R | undefined; refresh: () => void };

/**
 * One Loop Analysis tab: the request the filter makes of it, the query it last fetched with, and
 * that query's response and state. A tab is fetched only once it has been on screen, keeps its
 * response while another tab is shown, and is fetched again only when its request changed.
 */
export class LoopAnalysisTabState<Q, R extends LoopAnalysisResponse> {
  /** The query last asked for; undefined until the tab has been on screen. */
  query = $state.raw<Q>();
  response = $state.raw<R>();
  /** `response` answers a superseded query. */
  stale = $state(false);
  failed = $state(false);

  constructor(
    readonly label: string,
    private readonly request: () => Q,
    private readonly fetch: (query: Q) => Loaded<R>
  ) {}

  /** Takes up the filter's request while the tab is on screen; a request equal to the last is kept. */
  follow(): void {
    const next = this.request();
    untrack(() => {
      if (this.query !== undefined && JSON.stringify(this.query) === JSON.stringify(next)) return;
      this.query = next;
      if (this.response !== undefined || this.failed) this.stale = true;
    });
  }

  /**
   * Fetches the tab's query for the component that calls it, reporting to the report's resource
   * guard only while `active`, so a hidden tab's failure never blanks the one shown.
   */
  mount(active: () => boolean): void {
    const resource = contextResource(() => this.fetch(this.query ?? this.request()), {
      errorTitle: "Error Loading Loop Analysis",
      active,
    });
    $effect.pre(() => {
      const current = resource.current;
      const stale = resource.stale;
      const failed = resource.error != null;
      untrack(() => {
        this.response = current;
        this.stale = stale;
        this.failed = failed;
      });
    });
  }
}
