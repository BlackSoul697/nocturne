import { describe, expect, it } from 'vitest';
import { DOCS_SECTION_IDS, VIDEO_IDS, allowedProps, pageviewUrl } from './analytics';
import { DOCS_NAV_SECTIONS } from './data/docs-nav';

describe('allowedProps', () => {
  it('keeps a value the event declares', () => {
    expect(allowedProps('Support Tier Click', { tier: 'patron' })).toEqual({ tier: 'patron' });
  });

  it('drops a value outside the declared set', () => {
    expect(allowedProps('Support Tier Click', { tier: 'benefactor' })).toEqual({});
  });

  it('drops a key the event does not declare', () => {
    expect(allowedProps('Donate Click', { email: 'someone@example.com' })).toEqual({});
  });

  it('drops a key inherited from Object.prototype', () => {
    expect(allowedProps('Donate Click', { constructor: 'anything' })).toEqual({});
  });

  it('keeps the declared key and drops the rest', () => {
    expect(allowedProps('Docs Copy', { kind: 'code', page: '/docs/installation' })).toEqual({
      kind: 'code',
    });
  });
});

describe('docs videos', () => {
  it('reports a published video by id', () => {
    expect(VIDEO_IDS).toContain('agp');
    expect(allowedProps('Video Play', { video: 'agp' })).toEqual({ video: 'agp' });
    expect(allowedProps('Video Complete', { video: 'agp' })).toEqual({ video: 'agp' });
  });

  it('drops an id the video manifest does not hold', () => {
    expect(allowedProps('Video Complete', { video: 'not-a-video' })).toEqual({});
  });
});

describe('pageviewUrl', () => {
  it('keeps the path and drops an unlisted query parameter', () => {
    expect(pageviewUrl(new URL('https://getnocturne.dev/docs?locale=de'))).toBe(
      'https://getnocturne.dev/docs',
    );
  });

  it('keeps campaign parameters', () => {
    expect(
      pageviewUrl(new URL('https://getnocturne.dev/?utm_source=discord&utm_medium=chat')),
    ).toBe('https://getnocturne.dev/?utm_source=discord&utm_medium=chat');
  });

  it('drops the fragment', () => {
    expect(pageviewUrl(new URL('https://getnocturne.dev/get-involved#donate'))).toBe(
      'https://getnocturne.dev/get-involved',
    );
  });
});

describe('docs navigation', () => {
  // `DocsNavSection.id` is typed against DOCS_SECTION_IDS, but this package's `check` script
  // typechecks neither .ts nor .svelte, so nothing in CI enforces that binding. This does.
  it('reports every sidebar section', () => {
    const sidebar = DOCS_NAV_SECTIONS.map((section) => section.id).sort();
    expect(sidebar).toEqual([...DOCS_SECTION_IDS].sort());
  });

  it('declares each section once', () => {
    const ids = DOCS_NAV_SECTIONS.map((section) => section.id);
    expect(new Set(ids).size).toBe(ids.length);
  });
});

describe('an event name that is not in the table', () => {
  it('reports no properties instead of throwing', () => {
    const unknown = 'Docs Navigation' as Parameters<typeof allowedProps>[0];
    expect(() => allowedProps(unknown, { section: 'sdks' })).not.toThrow();
    expect(allowedProps(unknown, { section: 'sdks' })).toEqual({});
  });
});
