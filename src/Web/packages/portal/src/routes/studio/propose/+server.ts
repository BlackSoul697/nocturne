import { dev } from '$app/environment';
import { env } from '$env/dynamic/private';
import { json, error } from '@sveltejs/kit';
import type { RequestHandler } from './$types';

export const prerender = false;

/**
 * Dev-only bridge from the Studio to the content-contribution relay: the
 * portal is a static site with no production server, so proposing a PR from
 * the Studio goes through this dev-server endpoint, which forwards to
 * nocturne.run's anonymous content relay (or a local API via
 * CONTENT_CONTRIBUTION_URL when developing the flow end to end).
 */
const CONTENT_DIR_PREFIX = 'src/Web/packages/portal/src/content/blog';
const SLUG_PATTERN = /^[a-z0-9][a-z0-9-]*$/;

export const POST: RequestHandler = async ({ request, fetch }) => {
	if (!dev) {
		throw error(403, 'Studio propose API is only available in development mode');
	}

	const body = await request.json();
	const slug = String(body.slug ?? '');
	if (!SLUG_PATTERN.test(slug)) {
		throw error(400, 'Slug must be lowercase letters, digits and hyphens');
	}

	const target = env.CONTENT_CONTRIBUTION_URL || 'https://nocturne.run/api/v4/content/relay';
	const response = await fetch(target, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify({
			path: `${CONTENT_DIR_PREFIX}/${slug}.svx`,
			content: String(body.content ?? ''),
			title: String(body.title ?? slug),
			contributor: body.contributor,
			note: body.note ?? null,
		}),
	});

	if (!response.ok) {
		const detail = await response.text().catch(() => '');
		throw error(response.status === 422 ? 422 : 502, detail || 'The contribution was rejected');
	}

	return json(await response.json());
};
