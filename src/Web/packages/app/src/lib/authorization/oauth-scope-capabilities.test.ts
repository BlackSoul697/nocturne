import { describe, expect, it } from "vitest";
import { canDeleteRecords } from "./oauth-scope-capabilities";

describe("canDeleteRecords", () => {
  it.each([
    ["full access", ["*"], true],
    ["one category's read-write access", ["treatments.readwrite"], true],
    ["the health read-write alias", ["health.readwrite"], true],
    ["read-only access", ["treatments.read"], false],
    ["a device capability", ["device.actuate"], false],
  ] as const)("classifies %s", (_case, scopes, expected) => {
    expect(canDeleteRecords(scopes)).toBe(expected);
  });
});
