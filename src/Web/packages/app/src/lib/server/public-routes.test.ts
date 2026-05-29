import { describe, it, expect } from "vitest";
import { isPublicRoute, shouldRedirectToLogin } from "./public-routes";

describe("isPublicRoute", () => {
  it("does NOT treat the dashboard root as public", () => {
    // Regression: "/" used to be hardcoded public, which let unauthenticated
    // users reach the dashboard data load instead of being redirected to login.
    expect(isPublicRoute("/")).toBe(false);
  });

  it.each([
    "/auth/login",
    "/auth/recovery",
    "/api/v4/Status",
    "/setup",
    "/clock",
    "/invite/abc",
    "/terms",
    "/privacy",
    "/guest/47W-VC8F",
  ])("treats %s as public", (path) => {
    expect(isPublicRoute(path)).toBe(true);
  });

  it.each(["/_app/immutable/chunk.js", "/assets/logo.svg", "/favicon.ico"])(
    "treats static asset %s as public",
    (path) => {
      expect(isPublicRoute(path)).toBe(true);
    },
  );

  it("treats other protected routes as non-public", () => {
    expect(isPublicRoute("/reports")).toBe(false);
    expect(isPublicRoute("/settings")).toBe(false);
  });
});

describe("shouldRedirectToLogin", () => {
  it("redirects an unauthenticated visitor on the dashboard root when auth is required", () => {
    expect(
      shouldRedirectToLogin({
        pathname: "/",
        search: "",
        requireAuthentication: true,
        isAuthenticated: false,
      }),
    ).toBe("/auth/login?returnUrl=%2F");
  });

  it("preserves the original path and query in returnUrl", () => {
    expect(
      shouldRedirectToLogin({
        pathname: "/reports",
        search: "?range=7d",
        requireAuthentication: true,
        isAuthenticated: false,
      }),
    ).toBe("/auth/login?returnUrl=%2Freports%3Frange%3D7d");
  });

  it("fails closed: redirects when requireAuthentication could not be determined and defaulted to true", () => {
    // Mirrors the hook's catch block, which sets requireAuthentication = true
    // when the status probe errors or times out (privacy-preserving default).
    expect(
      shouldRedirectToLogin({
        pathname: "/",
        search: "",
        requireAuthentication: true,
        isAuthenticated: false,
      }),
    ).not.toBeNull();
  });

  it("allows an already-authenticated user through", () => {
    expect(
      shouldRedirectToLogin({
        pathname: "/",
        search: "",
        requireAuthentication: true,
        isAuthenticated: true,
      }),
    ).toBeNull();
  });

  it("allows public read-only instances through (requireAuthentication false)", () => {
    expect(
      shouldRedirectToLogin({
        pathname: "/",
        search: "",
        requireAuthentication: false,
        isAuthenticated: false,
      }),
    ).toBeNull();
  });

  it("does not redirect undefined requireAuthentication (only the explicit fail-closed default gates)", () => {
    expect(
      shouldRedirectToLogin({
        pathname: "/",
        search: "",
        requireAuthentication: undefined,
        isAuthenticated: false,
      }),
    ).toBeNull();
  });

  it("never redirects public routes even when auth is required", () => {
    expect(
      shouldRedirectToLogin({
        pathname: "/auth/login",
        search: "",
        requireAuthentication: true,
        isAuthenticated: false,
      }),
    ).toBeNull();
    expect(
      shouldRedirectToLogin({
        pathname: "/guest/47W-VC8F",
        search: "",
        requireAuthentication: true,
        isAuthenticated: false,
      }),
    ).toBeNull();
  });
});
