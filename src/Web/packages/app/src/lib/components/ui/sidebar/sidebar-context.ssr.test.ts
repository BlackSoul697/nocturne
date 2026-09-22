import { describe, expect, it } from "vitest";
import { render } from "svelte/server";
import SidebarContextProbe from "$lib/test-fixtures/SidebarContextProbe.svelte";

/**
 * The sidebar context has to survive the translation transform.
 *
 * wuchale extracts messages out of component scripts, and where the message
 * sits inside a top-level declaration's initialiser the Svelte adapter wraps
 * that initialiser in `$derived`. `$derived` is lazy on the server, so an
 * initialiser that exists for its side effect — `setSidebar()`, which calls
 * setContext — stops running there. Nothing else catches it: the build is
 * green, the components pass their own tests, and the failure only appears
 * when a page is rendered.
 */
describe("sidebar context under the translation transform", () => {
	it("is set by the provider and readable by a descendant", () => {
		const { body } = render(SidebarContextProbe, { props: { marker: "probe" } });

		// Reaching a value at all means the provider's setContext ran: the
		// consumer dereferences what useSidebar() returned, so a missing
		// context throws instead of rendering.
		expect(body).toContain("probe:false");
	});
});
