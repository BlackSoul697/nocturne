import { redirect } from "@sveltejs/kit";
import { LEGACY_TABS, loopAnalysisPath } from "$lib/components/reports/loop-analysis/loop-analysis-pages.svelte";
import type { PageLoad } from "./$types";

/** The report is five pages; a link to the one page it was opens the page its `tab` showed. */
export const load: PageLoad = ({ url }) => {
  const search = new URLSearchParams(url.searchParams);
  const target = LEGACY_TABS[search.get("tab") ?? ""] ?? "daily-profile";
  search.delete("tab");
  redirect(307, loopAnalysisPath(target, search));
};
