export function canDeleteRecords(scopes: readonly string[]): boolean {
  return scopes.some(
    (scope) =>
      scope === "*" ||
      scope === "health.readwrite" ||
      scope.endsWith(".readwrite")
  );
}
