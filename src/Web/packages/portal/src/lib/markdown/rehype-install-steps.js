/**
 * Marks the headings of a markdown installation guide for `Install Step`, which a markdown
 * heading cannot carry itself. The page's frontmatter maps heading ids (as rehype-slug writes
 * them, so this runs after it) to step ids:
 *
 *     installSteps:
 *       the-three-headers: "3"
 *       nginx: nginx
 *
 * A listed id that matches no heading fails the build, so renaming a heading cannot silently
 * stop its step being counted.
 */
export function rehypeInstallSteps() {
  return (tree, file) => {
    const steps = file.data.fm?.installSteps;
    if (!steps) return;

    const unmatched = new Set(Object.keys(steps));
    const visit = (node) => {
      if (node.type === 'element' && /^h[1-6]$/.test(node.tagName)) {
        const id = node.properties?.id;
        if (typeof id === 'string' && Object.hasOwn(steps, id)) {
          node.properties.dataInstallStep = String(steps[id]);
          unmatched.delete(id);
        }
      }
      for (const child of node.children ?? []) visit(child);
    };
    visit(tree);

    if (unmatched.size > 0) {
      throw new Error(
        `${file.path ?? 'A docs page'}: installSteps names no heading with id ${[...unmatched].join(', ')}`,
      );
    }
  };
}
