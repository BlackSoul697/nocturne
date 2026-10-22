import { untrack } from "svelte";
import type { LoopAnalysisResponse } from "$lib/api";
import { contextResource } from "$lib/hooks/resource-context.svelte";

type Loaded<R> = { loading: boolean; error: unknown; current: R | undefined; refresh: () => void };
type Resource<R> = { readonly current: R | undefined; readonly stale: boolean; readonly error: unknown };

/**
 * One Loop Analysis tab: the request the filter makes of it, the query it last fetched with, and
 * that query's response and state. A tab is fetched only once it has been on screen, keeps its
 * response while another tab is shown, and is fetched again only when its request changed.
 */
export class LoopAnalysisTabState<Q, R extends LoopAnalysisResponse> {
  /** The query last asked for; undefined until the tab has been on screen. */
  query = $state.raw<Q>();
  #resource = $state.raw<Resource<R>>();

  constructor(
    readonly label: string,
    private readonly request: () => Q,
    private readonly fetch: (query: Q) => Loaded<R>
  ) {}

  get response(): R | undefined {
    return this.#resource?.current;
  }

  /** `response` answers a superseded query. */
  get stale(): boolean {
    return this.#resource?.stale ?? false;
  }

  get failed(): boolean {
    return this.#resource?.error != null;
  }

  /** Takes up the filter's request while the tab is on screen; a request equal to the last is kept. */
  follow(): void {
    const next = this.request();
    untrack(() => {
      if (this.query !== undefined && JSON.stringify(this.query) === JSON.stringify(next)) return;
      this.query = next;
    });
  }

  /**
   * Fetches the tab's query for the component that calls it. It reports to the report's resource
   * guard only while `active` and once it has a response or an error, so neither a hidden tab's
   * failure nor a tab's first load blanks the page: the tab shows its own loading state instead.
   */
  mount(active: () => boolean): void {
    this.#resource = contextResource(() => this.fetch(this.query ?? this.request()), {
      errorTitle: "Error Loading Loop Analysis",
      active: () => active() && (this.response !== undefined || this.failed),
    });
  }
}
