import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { NocturneApiClient } from "./api-client.js";
import type { SimulatorConfig } from "./config.js";

describe("NocturneApiClient", () => {
  const baseConfig: SimulatorConfig = {
    enabled: true,
    apiUrl: "http://localhost:1610",
    instanceKey: "test-instance-key-long-enough-32-chars-long",
    baseDomain: "nocturne.localhost",
    intervalMs: 300000,
    serviceName: "cgmsim",
    allowDevFallback: true,
  };

  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it("sends X-Forwarded-Host, X-Forwarded-Proto and X-Instance-Key for tenant calls", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify([{ _id: "1", sgv: 120, date: Date.now() }]), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      })
    );
    globalThis.fetch = fetchMock;

    const client = new NocturneApiClient(baseConfig);
    const treatments = await client.getRecentTreatments("lorenzo", new Date().toISOString());

    expect(treatments).toHaveLength(1);
    expect(fetchMock).toHaveBeenCalledTimes(1);

    const callArgs = fetchMock.mock.calls[0];
    const headers = callArgs[1]?.headers as Record<string, string>;

    expect(headers["X-Forwarded-Host"]).toBe("lorenzo.nocturne.localhost");
    expect(headers["X-Forwarded-Proto"]).toBe("https");
    expect(headers["X-Instance-Service"]).toBe("cgmsim");
    expect(headers["X-Instance-Key"]).toBeDefined();
  });

  it("fetches dev token via dev-only login endpoint when instance key is missing", async () => {
    const devConfig: SimulatorConfig = {
      ...baseConfig,
      instanceKey: "",
    };

    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes("/api/v4/dev-only/auth/login")) {
        return Promise.resolve(
          new Response(
            JSON.stringify({
              accessToken: "mocked-dev-access-token",
              expiresInSeconds: 3600,
            }),
            { status: 200, headers: { "Content-Type": "application/json" } }
          )
        );
      }

      if (url.includes("/api/v1/entries")) {
        return Promise.resolve(new Response(JSON.stringify({ ok: true }), { status: 200 }));
      }

      return Promise.resolve(new Response("Not Found", { status: 404 }));
    });
    globalThis.fetch = fetchMock;

    const client = new NocturneApiClient(devConfig);
    const success = await client.postSimulatedEntry("lorenzo", {
      type: "sgv",
      sgv: 115,
      direction: "Flat",
      date: Date.now(),
      dateString: new Date().toISOString(),
      device: "cgmsim-node",
    });

    expect(success).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(2);

    // First call: dev login
    const loginCall = fetchMock.mock.calls[0];
    expect(loginCall[0]).toContain("/api/v4/dev-only/auth/login");

    // Second call: post entries with bearer token and X-Forwarded-Host
    const postCall = fetchMock.mock.calls[1];
    const headers = postCall[1]?.headers as Record<string, string>;
    expect(headers["Authorization"]).toBe("Bearer mocked-dev-access-token");
    expect(headers["X-Forwarded-Host"]).toBe("lorenzo.nocturne.localhost");
    expect(headers["X-Forwarded-Proto"]).toBe("https");
  });
});
