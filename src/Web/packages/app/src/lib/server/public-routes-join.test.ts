import { describe, it, expect } from "vitest";
import { statusProbeRedirect } from "./public-routes";

describe("statusProbeRedirect for /join", () => {
  const base = {
    isShareHost: false,
    recoveryMode: false,
    marketingUrl: undefined,
  };

  it("allows /join to render when the tenant is still in setup_required", () => {
    expect(
      statusProbeRedirect({
        ...base,
        apiStatus: 503,
        errorCode: "setup_required",
        allowSetupRequired: true,
      })
    ).toBeNull();
  });

  it("still redirects /join to /auth/recovery when recoveryMode is active", () => {
    expect(
      statusProbeRedirect({
        ...base,
        apiStatus: 503,
        recoveryMode: true,
        errorCode: "recovery_mode_active",
        allowSetupRequired: true,
      })
    ).toEqual({ location: "/auth/recovery", status: 303 });
  });

  it("still redirects unknown tenants on /join to marketingUrl or /setup", () => {
    expect(
      statusProbeRedirect({
        ...base,
        apiStatus: 404,
        marketingUrl: "https://nocturne.run",
        allowSetupRequired: true,
      })
    ).toEqual({ location: "https://nocturne.run", status: 302 });
  });
});

