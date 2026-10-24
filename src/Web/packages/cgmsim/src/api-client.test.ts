import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { NocturneApiClient, isNightscoutConnector } from "./api-client.js";
import type { SimulatorConfig } from "./config.js";

describe("isNightscoutConnector", () => {
  it("detects Nightscout connectors across dev-only and production DTO formats", () => {
    // Dev-only DTO property: 'name'
    expect(isNightscoutConnector({ name: "Nightscout" })).toBe(true);
    expect(isNightscoutConnector({ name: "nightscout" })).toBe(true);
    expect(isNightscoutConnector({ name: "nightscout-connector" })).toBe(true);

    // Platform-admin DTO property: 'connectorName'
    expect(isNightscoutConnector({ connectorName: "Nightscout" })).toBe(true);
    expect(isNightscoutConnector({ connectorName: "nightscout" })).toBe(true);
    expect(isNightscoutConnector({ connectorName: "nightscout-connector" })).toBe(true);

    // Other connectors
    expect(isNightscoutConnector({ name: "dexcom" })).toBe(false);
    expect(isNightscoutConnector({ connectorName: "carelink" })).toBe(false);
    expect(isNightscoutConnector({})).toBe(false);
  });
});

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

  it("filters out tenants with Nightscout connector in dev-only admin endpoint", async () => {
    const tenantsPayload = [
      {
        id: "tenant-1",
        slug: "lorenzo",
        displayName: "Lorenzo",
        isActive: true,
        connectors: [
          { name: "Nightscout", isHealthy: true, lastSuccessfulSync: "2026-10-24T18:00:00Z" },
        ],
      },
      {
        id: "tenant-2",
        slug: "mario",
        displayName: "Mario",
        isActive: true,
        connectors: [],
      },
      {
        id: "tenant-3",
        slug: "giulia",
        displayName: "Giulia",
        isActive: true,
        connectors: [{ name: "dexcom", isHealthy: true }],
      },
      {
        id: "tenant-4",
        slug: "inactive-user",
        displayName: "Inactive User",
        isActive: false,
        connectors: [],
      },
    ];

    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes("/api/v4/dev-only/admin/tenants")) {
        return Promise.resolve(
          new Response(JSON.stringify(tenantsPayload), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          })
        );
      }
      return Promise.resolve(new Response("Not Found", { status: 404 }));
    });
    globalThis.fetch = fetchMock;

    const client = new NocturneApiClient(baseConfig);
    const result = await client.getActiveTenantsWithoutNightscout();

    // lorenzo (Nightscout) and inactive-user (inactive) should be excluded
    expect(result.map((t) => t.slug)).toEqual(["mario", "giulia"]);
  });

  it("filters out tenants with Nightscout connector in platform-admin fallback", async () => {
    const prodConfig: SimulatorConfig = {
      ...baseConfig,
      allowDevFallback: false,
    };

    const tenantsPayload = [
      { id: "tenant-1", slug: "lorenzo", displayName: "Lorenzo", isActive: true },
      { id: "tenant-2", slug: "mario", displayName: "Mario", isActive: true },
    ];

    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.endsWith("/api/v4/admin/tenants")) {
        return Promise.resolve(
          new Response(JSON.stringify(tenantsPayload), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          })
        );
      }
      if (url.includes("/api/v4/admin/connectors/tenant-1")) {
        return Promise.resolve(
          new Response(
            JSON.stringify({
              connectors: [{ connectorName: "nightscout", isHealthy: true }],
            }),
            { status: 200, headers: { "Content-Type": "application/json" } }
          )
        );
      }
      if (url.includes("/api/v4/admin/connectors/tenant-2")) {
        return Promise.resolve(
          new Response(
            JSON.stringify({
              connectors: [],
            }),
            { status: 200, headers: { "Content-Type": "application/json" } }
          )
        );
      }
      return Promise.resolve(new Response("Not Found", { status: 404 }));
    });
    globalThis.fetch = fetchMock;

    const client = new NocturneApiClient(prodConfig);
    const result = await client.getActiveTenantsWithoutNightscout();

    expect(result.map((t) => t.slug)).toEqual(["mario"]);
  });
});
